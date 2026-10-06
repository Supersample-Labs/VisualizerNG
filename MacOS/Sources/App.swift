import SwiftUI
import AppKit

let modes = ["Oscilloscope", "Circular oscilloscope", "Triangle oscilloscope", "Square oscilloscope", "Pentagon oscilloscope", "Hexagon oscilloscope", "Octagon oscilloscope", "Star oscilloscope", "Spectrum", "Radial spectrum"]
let paletteNames = ["Lagoon", "Electric blue", "Neon violet", "Sunset", "Lime"]
let primaries: [Color] = [Color(red:0.25,green:0.92,blue:0.86), Color(red:0.35,green:0.64,blue:1), Color(red:0.74,green:0.38,blue:1), Color(red:1,green:0.43,blue:0.24), Color(red:0.55,green:0.95,blue:0.25)]
let accents: [Color] = [Color(red:0.65,green:1,blue:0.95), .yellow, .pink, .yellow, Color(red:0.9,green:1,blue:0.65)]

@MainActor final class Model: ObservableObject {
    @Published var mode = 0
    @Published var palettes = [0,0,0,0,0,0,0,0,1,0]
    @Published var smoothing = 0.72
    @Published var sensitivity = 2.0
    @Published var rotation = 0.0
    @Published var spin = false
    @Published var rotationMode = 0
    @Published var angle = 0.0
    @Published var tilt = 0.0
    @Published var turn = 0.0
    @Published var tiltSpin = 0.0
    @Published var turnSpin = 0.0
    @Published var crtMode = 0
    @Published var snowIntensity = 0.3
    @Published var interlaceIntensity = 0.5
    @Published var fullscreen = false
    @Published var running = false
    @Published var busy = false
    @Published var demo = false
    @Published var status = "Click Start to visualize system playback."
    @Published var features = Features()
    @Published var peaks = [Float](repeating: 0, count: 64)
    let analyzer = Analyzer()
    lazy var capture = AudioCapture(analyzer: analyzer)
    private var timer: Timer?
    private var phase = 0.0
    init() {
        capture.onError = { [weak self] message in DispatchQueue.main.async {
            self?.running = false; self?.status = message
        }}
        timer = Timer.scheduledTimer(withTimeInterval: 1.0/60, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated { self?.tick() }
        }
    }
    func tick() {
        if demo {
            let values: [Float] = (0..<800).map { i in
                let t = phase + Double(i)/48000
                return Float(0.18 * sin(2 * .pi * 110 * t) + 0.09 * sin(2 * .pi * 880 * t)) * Float(0.6 + 0.4 * sin(t * 3))
            }
            phase += 800.0/48000; analyzer.append(values)
        }
        features = analyzer.analyze(smoothing: Float(smoothing), sensitivity: Float(sensitivity))
        for i in 0..<64 { peaks[i] = max(features.bars[i], peaks[i] - 0.28/60) }
        if rotationMode > 0 { angle = (angle + 24.0/60).truncatingRemainder(dividingBy: 360) }
        if rotationMode == 2 {
            tiltSpin = (tiltSpin + 17.0/60).truncatingRemainder(dividingBy: 360)
            turnSpin = (turnSpin + 29.0/60).truncatingRemainder(dividingBy: 360)
        }
        if running { status = analyzer.receiving ? "Receiving system audio" : "Listening — play audio in another app." }
    }
    func toggleCapture() {
        guard !busy else { return }
        busy = true
        Task {
            do {
                if running { try await capture.stop(); running = false; status = "Capture stopped." }
                else { demo = false; analyzer.clear(); try await capture.start(); running = true }
            } catch { status = "Capture failed: \(error.localizedDescription). Allow Screen & System Audio Recording in System Settings, then restart this app." }
            busy = false
        }
    }
    func toggleFullscreen() { NSApp.keyWindow?.toggleFullScreen(nil) }
}

struct Visual: View {
    @ObservedObject var model: Model
    var body: some View {
        Canvas { context, size in
            let palette = model.palettes[model.mode]
            let color = primaries[palette], accent = accents[palette]
            let radius = min(size.width,size.height) * 0.44
            context.translateBy(x: size.width/2, y: size.height/2)
            let radians = (model.rotationMode > 0 ? model.rotation + model.angle : 0) * .pi / 180
            context.rotate(by: .radians(radians))
            if model.rotationMode == 2 {
                let tilt = (model.tilt + model.tiltSpin) * .pi / 180
                let turn = (model.turn + model.turnSpin) * .pi / 180
                // Affine projection of a plane rotated in 3D, with perspective-like depth.
                let sx = cos(turn), sy = cos(tilt)
                context.concatenate(CGAffineTransform(a: sx, b: sin(turn)*sin(tilt), c: 0, d: sy, tx: 0, ty: 0))
            }
            let linear = model.mode == 0 || model.mode == 8
            let w = size.width * 0.90, h = size.height * 0.82
            if linear {
                let bw = abs(cos(radians))*w + abs(sin(radians))*h
                let bh = abs(sin(radians))*w + abs(cos(radians))*h
                let fit = min(1, min(size.width*0.95/max(1,bw),size.height*0.95/max(1,bh)))
                context.scaleBy(x:fit,y:fit)
            }
            if model.mode == 8 {
                let slot = w/64
                for i in 0..<64 {
                    let height = max(2, CGFloat(model.features.bars[i])*h)
                    let x = -w/2 + CGFloat(i)*slot
                    context.fill(Path(CGRect(x:x,y:h/2-height,width:slot*0.72,height:height)),with:.color(color))
                    context.fill(Path(CGRect(x:x,y:h/2-CGFloat(model.peaks[i])*h-2,width:slot*0.72,height:2)),with:.color(accent))
                }
            } else if model.mode == 9 {
                context.stroke(Path(ellipseIn:CGRect(x:-radius*0.4,y:-radius*0.4,width:radius*0.8,height:radius*0.8)),with:.color(accent),lineWidth:2)
                for i in 0..<64 {
                    let a = Double(i)/64 * 2 * Double.pi
                    let inner = radius*0.44, outer = inner + max(2,CGFloat(model.features.bars[i])*radius*0.55)
                    var p = Path(); p.move(to:CGPoint(x:cos(a)*inner,y:sin(a)*inner)); p.addLine(to:CGPoint(x:cos(a)*outer,y:sin(a)*outer))
                    context.stroke(p,with:.color(color),lineWidth: max(2,radius*0.022))
                }
            } else {
                var path = Path()
                let count = model.features.wave.count
                for i in 0..<count {
                    let t = Double(i)/Double(count-1)
                    let sample = Double(model.features.wave[i])
                    var point: CGPoint
                    if model.mode == 0 { point = CGPoint(x:(t-0.5)*w,y:-sample*h*0.45) }
                    else {
                        let a = t*2*Double.pi - Double.pi/2
                        let shapeSides = [0,0,3,4,5,6,8,10][model.mode]
                        let star = model.mode == 7
                        var base = 0.625
                        if shapeSides > 0 {
                            let sides = star ? shapeSides * 2 : shapeSides
                            let sector = 2 * Double.pi / Double(sides)
                            if star {
                                let pos = t * Double(sides), edge = Int(pos) % sides
                                let phase = (pos - floor(pos)) * sector
                                let start = edge % 2 == 0 ? 0.72 : 0.34, end = edge % 2 == 0 ? 0.34 : 0.72
                                base = start * end * sin(sector) / (end * sin(sector-phase) + start * sin(phase))
                            } else {
                                let phase = (t*2*Double.pi + (shapeSides == 4 ? sector/2 : 0)).truncatingRemainder(dividingBy:sector)
                                base = 0.773 * cos(Double.pi/Double(shapeSides))/cos(phase-sector/2)
                            }
                        }
                        // Apply the waveform around the complete closed outline.
                        // A half-wave taper here left the start/end of the contour
                        // nearly fixed, which read as a section that never moved.
                        let r = radius * min(1,max(0.136,base+sample*0.74))
                        point = CGPoint(x:cos(a)*r,y:sin(a)*r)
                    }
                    if i == 0 { path.move(to:point) } else { path.addLine(to:point) }
                }
                if model.mode != 0 { path.closeSubpath() }
                context.stroke(path,with:.color(color.opacity(0.12)),lineWidth:9)
                context.stroke(path,with:.color(color),lineWidth:2)
            }
        }.background(Color(red:0.025,green:0.035,blue:0.055))
        .overlay {
            Canvas { context, size in
                if model.crtMode != 0 { drawCRT(&context, size: size, model: model) }
            }.allowsHitTesting(false)
        }
    }

    private func drawCRT(_ context: inout GraphicsContext, size: CGSize, model: Model) {
        let snow = model.crtMode == 1 || model.crtMode == 3 ? model.snowIntensity : 0
        let scan = model.crtMode == 2 || model.crtMode == 3 ? model.interlaceIntensity : 0
        let time = Date.timeIntervalSinceReferenceDate
        if snow > 0 {
            for _ in 0..<Int(size.width * size.height / 900 * snow) {
                let x = CGFloat.random(in: 0...max(1,size.width)), y = CGFloat.random(in: 0...max(1,size.height))
                let alpha = Double.random(in: 0.08...0.55) * snow
                context.fill(Path(CGRect(x:x,y:y,width:1.5,height:1.5)),with:.color(.white.opacity(alpha)))
            }
        }
        if scan > 0 {
            for row in stride(from: CGFloat(0), through: size.height, by: CGFloat(4)) {
                context.fill(Path(CGRect(x:0,y:row,width:size.width,height:2)),with:.color(.black.opacity(0.22*scan)))
            }
            for band in 0..<3 {
                let y = (CGFloat((time * 0.18).truncatingRemainder(dividingBy: 1)) * size.height + CGFloat(band) * size.height/3).truncatingRemainder(dividingBy:size.height)
                context.fill(Path(CGRect(x:0,y:y-18,width:size.width,height:36)),with:.color(Color(red:0.45,green:0.65,blue:0.72).opacity(0.11*scan)))
            }
        }
    }
}

struct ContentView: View {
    @StateObject private var model = Model()
    var body: some View {
        VStack(spacing:0) {
            Visual(model:model).frame(maxWidth:.infinity,maxHeight:.infinity)
            if !model.fullscreen { ControlsView(model:model) }
        }.frame(minWidth:850,minHeight:560)
        .preferredColorScheme(.dark)
        .onReceive(NotificationCenter.default.publisher(for:NSWindow.didEnterFullScreenNotification)) { _ in model.fullscreen = true }
        .onReceive(NotificationCenter.default.publisher(for:NSWindow.didExitFullScreenNotification)) { _ in model.fullscreen = false }
        .onExitCommand { if model.fullscreen { model.toggleFullscreen() } }
        .background(KeyHandler(action:{ model.toggleFullscreen() }))
    }
}

private struct ControlsView: View {
    @ObservedObject var model: Model
    var body: some View {
        VStack(alignment:.leading,spacing:12) {
            HStack {
                Picker("Visualizer",selection:$model.mode) { ForEach(0..<modes.count,id:\.self) { Text(modes[$0]).tag($0) } }.frame(width:290)
                Picker("Colors",selection:Binding(get:{model.palettes[model.mode]},set:{model.palettes[model.mode] = $0})) { ForEach(0..<5,id:\.self) { Text(paletteNames[$0]).tag($0) } }.frame(width:210)
                Spacer()
                Button("Fullscreen") { model.toggleFullscreen() }.keyboardShortcut(.init("f"),modifiers:[.command,.control])
            }
            HStack(spacing:20) {
                VStack { Text("Sensitivity \(model.sensitivity,specifier:"%.1f")"); Slider(value:$model.sensitivity,in:0.2...8) }
                VStack { Text("Smoothing \(model.smoothing,specifier:"%.2f")"); Slider(value:$model.smoothing,in:0...0.98) }
                RotationControls(model:model)
                CRTControls(model:model)
            }
            HStack(spacing:18) {
                ForEach(0..<4,id:\.self) { i in
                    VStack(alignment:.leading) { Text(["Level","Bass","Mid","Treble"][i]).font(.caption); ProgressView(value:Double(model.features.levels[i])).tint(primaries[model.palettes[model.mode]]) }
                }
            }
            HStack {
                Button(model.busy ? "Please wait…" : (model.running ? "Stop" : "Start")) { model.toggleCapture() }.disabled(model.busy)
                Toggle("Demo",isOn:$model.demo).disabled(model.running || model.busy).onChange(of:model.demo) { value in model.analyzer.clear(); model.status = value ? "Demo signal — no audio is captured or played." : "Click Start to visualize system playback." }
                Text(model.status).font(.caption).foregroundStyle(.secondary).lineLimit(2).help(model.status)
                Spacer()
                Button("About",action:showAbout)
            }
        }.padding(16).background(.regularMaterial)
    }
    private func showAbout() {
        let alert = NSAlert(); alert.messageText = "VisualizerNG for macOS"
        alert.informativeText = "Native macOS port. Audio is analyzed in memory and is never saved or sent anywhere. Uses Apple ScreenCaptureKit, SwiftUI and Accelerate. System audio permission is required; microphone access is not used."
        alert.runModal()
    }
}

private struct RotationControls: View {
    @ObservedObject var model: Model
    var body: some View {
        VStack {
            Text("Rotation")
            Picker("Rotation",selection:$model.rotationMode) { Text("Off").tag(0); Text("2D spin").tag(1); Text("3D spin").tag(2) }.labelsHidden()
            Text("Angle \(Int(model.rotation))°")
            Slider(value:$model.rotation,in:0...360,step:15).disabled(model.rotationMode == 0)
            Text("Tilt \(Int(model.tilt))°")
            Slider(value:$model.tilt,in:-180...180,step:15).disabled(model.rotationMode != 2)
            Text("Turn \(Int(model.turn))°")
            Slider(value:$model.turn,in:-180...180,step:15).disabled(model.rotationMode != 2)
            Button("Reset rotation") {
                model.rotation = 0; model.angle = 0; model.tilt = 0; model.turn = 0
                model.tiltSpin = 0; model.turnSpin = 0; model.rotationMode = 0; model.spin = false
            }
        }
    }
}

private struct CRTControls: View {
    @ObservedObject var model: Model
    var body: some View {
        VStack {
            Text("CRT effects")
            Picker("CRT effects",selection:$model.crtMode) {
                Text("Off").tag(0); Text("Snow").tag(1); Text("Interlace").tag(2); Text("Snow + Interlace").tag(3)
            }.labelsHidden()
            Text("Snow \(Int(model.snowIntensity*100))%")
            Slider(value:$model.snowIntensity,in:0...1).disabled(model.crtMode != 1 && model.crtMode != 3)
            Text("Interlace \(Int(model.interlaceIntensity*100))%")
            Slider(value:$model.interlaceIntensity,in:0...1).disabled(model.crtMode != 2 && model.crtMode != 3)
        }
    }
}

struct KeyHandler: NSViewRepresentable {
    var action: () -> Void
    func makeCoordinator() -> Coordinator { Coordinator(action:action) }
    func makeNSView(context:Context) -> NSView { NSView() }
    func updateNSView(_ view:NSView,context:Context) {}
    final class Coordinator {
        var monitor: Any?
        init(action:@escaping () -> Void) { monitor = NSEvent.addLocalMonitorForEvents(matching:.keyDown) { event in if event.keyCode == 103 { action(); return nil }; return event } }
        deinit { if let monitor { NSEvent.removeMonitor(monitor) } }
    }
}

@main struct VisualizerApp: App {
    var body: some Scene {
        WindowGroup("VisualizerNG") { ContentView() }.commands { CommandGroup(replacing:.newItem) {} }
    }
}

