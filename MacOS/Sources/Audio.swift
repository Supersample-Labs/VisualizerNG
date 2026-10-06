import Foundation
import ScreenCaptureKit
import CoreMedia
import Accelerate

struct Features {
    var wave = [Float](repeating: 0, count: 1024)
    var bars = [Float](repeating: 0, count: 64)
    var levels = [Float](repeating: 0, count: 4)
}

final class Analyzer {
    private let lock = NSLock()
    private var samples = [Float](repeating: 0, count: 4096)
    private var cursor = 0
    private var lastSignal = Date.distantPast
    private var previous = Features()
    private let fft = vDSP_create_fftsetup(12, FFTRadix(kFFTRadix2))!
    deinit { vDSP_destroy_fftsetup(fft) }
    func append(_ values: [Float]) {
        lock.lock(); defer { lock.unlock() }
        for value in values { samples[cursor] = value.isFinite ? value : 0; cursor = (cursor + 1) % samples.count }
        if values.contains(where: { abs($0) > 0.0001 }) { lastSignal = Date() }
    }
    func clear() {
        lock.lock(); samples = Array(repeating: 0, count: 4096); cursor = 0; lastSignal = .distantPast; lock.unlock()
        previous = Features()
    }
    var receiving: Bool { lock.lock(); defer { lock.unlock() }; return Date().timeIntervalSince(lastSignal) < 2 }
    func analyze(smoothing: Float, sensitivity: Float) -> Features {
        lock.lock(); let input = Array(samples[cursor...] + samples[..<cursor]); lock.unlock()
        var real = [Float](repeating: 0, count: 4096), imaginary = real
        for i in 0..<4096 { real[i] = input[i] * Float(0.5 - 0.5 * cos(2 * Double.pi * Double(i) / 4095)) }
        real.withUnsafeMutableBufferPointer { r in imaginary.withUnsafeMutableBufferPointer { im in
            var split = DSPSplitComplex(realp: r.baseAddress!, imagp: im.baseAddress!)
            vDSP_fft_zip(fft, &split, 1, 12, FFTDirection(FFT_FORWARD))
        }}
        var spectrum = [Float](repeating: 0, count: 2048)
        for i in 0..<2048 { let power: Float = real[i] * real[i] + imaginary[i] * imaginary[i]; spectrum[i] = sqrt(power) / 1024 }
        func band(_ low: Double, _ high: Double) -> Float {
            let start = max(1, min(2047, Int(low * 4096 / 48000)))
            let end = max(start, min(2047, Int(ceil(high * 4096 / 48000))))
            return spectrum[start...end].reduce(0,+) / Float(end-start+1)
        }
        func smooth(_ old: Float, _ target: Float) -> Float { old * smoothing + min(1, max(0, target)) * (1-smoothing) }
        var result = Features()
        result.wave = (0..<1024).map { input[$0 * 4] }
        for i in 0..<64 {
            let low = 20 * pow(1000, Double(i)/64), high = 20 * pow(1000, Double(i+1)/64)
            result.bars[i] = smooth(previous.bars[i], band(low,high) * sensitivity * 1.8)
        }
        let rms = sqrt(input.reduce(0) { $0 + $1 * $1 } / 4096)
        let targets = [rms * 5.5, band(20,250) * 2.4, band(250,2000) * 2.2, band(2000,20000) * 2.8]
        result.levels = (0..<4).map { smooth(previous.levels[$0], targets[$0] * sensitivity) }
        previous = result
        return result
    }
}

final class AudioCapture: NSObject, SCStreamOutput, SCStreamDelegate {
    let analyzer: Analyzer
    private var activeStream: SCStream?
    var onError: ((String) -> Void)?
    init(analyzer: Analyzer) { self.analyzer = analyzer }
    @MainActor func start() async throws {
        let content = try await SCShareableContent.excludingDesktopWindows(false, onScreenWindowsOnly: true)
        guard let display = content.displays.first else { throw NSError(domain: "Visualizer", code: 1, userInfo: [NSLocalizedDescriptionKey: "No display is available for system audio capture."]) }
        let config = SCStreamConfiguration()
        config.capturesAudio = true
        config.excludesCurrentProcessAudio = true
        config.sampleRate = 48000
        config.channelCount = 2
        config.width = 2; config.height = 2
        config.minimumFrameInterval = CMTime(value: 1, timescale: 1)
        let stream = SCStream(filter: SCContentFilter(display: display, excludingWindows: []), configuration: config, delegate: self)
        try stream.addStreamOutput(self, type: .audio, sampleHandlerQueue: DispatchQueue(label: "visualizer.audio"))
        try stream.addStreamOutput(self, type: .screen, sampleHandlerQueue: DispatchQueue(label: "visualizer.discardVideo"))
        try await stream.startCapture()
        activeStream = stream
    }
    @MainActor func stop() async throws {
        guard let stream = activeStream else { return }
        try await stream.stopCapture()
        activeStream = nil
        analyzer.clear()
    }
    func stream(_ stream: SCStream, didStopWithError error: Error) { onError?(error.localizedDescription) }
    func stream(_ stream: SCStream, didOutputSampleBuffer buffer: CMSampleBuffer, of type: SCStreamOutputType) {
        guard type == .audio, buffer.isValid,
              let description = buffer.formatDescription,
              let format = CMAudioFormatDescriptionGetStreamBasicDescription(description)?.pointee,
              format.mFormatID == kAudioFormatLinearPCM,
              format.mFormatFlags & kAudioFormatFlagIsFloat != 0, format.mBitsPerChannel == 32 else { return }
        var size = 0
        guard CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer(buffer, bufferListSizeNeededOut: &size, bufferListOut: nil, bufferListSize: 0, blockBufferAllocator: nil, blockBufferMemoryAllocator: nil, flags: 0, blockBufferOut: nil) == noErr else { return }
        let memory = UnsafeMutableRawPointer.allocate(byteCount: size, alignment: MemoryLayout<AudioBufferList>.alignment)
        defer { memory.deallocate() }
        let list = memory.bindMemory(to: AudioBufferList.self, capacity: 1)
        var block: CMBlockBuffer?
        guard CMSampleBufferGetAudioBufferListWithRetainedBlockBuffer(buffer, bufferListSizeNeededOut: nil, bufferListOut: list, bufferListSize: size, blockBufferAllocator: nil, blockBufferMemoryAllocator: nil, flags: 0, blockBufferOut: &block) == noErr else { return }
        let buffers = UnsafeMutableAudioBufferListPointer(list)
        let count = CMSampleBufferGetNumSamples(buffer)
        var mono = [Float](repeating: 0, count: count)
        var channels = 0
        for audio in buffers {
            guard let data = audio.mData else { continue }
            let n = Int(audio.mNumberChannels)
            guard n > 0, Int(audio.mDataByteSize) >= count * n * 4 else { continue }
            let floats = data.assumingMemoryBound(to: Float.self)
            channels += n
            for frame in 0..<count { for channel in 0..<n { mono[frame] += floats[frame*n+channel] } }
        }
        guard channels > 0 else { return }
        analyzer.append(mono.map { $0 / Float(channels) })
    }
}
