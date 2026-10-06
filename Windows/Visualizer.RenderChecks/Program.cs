using OpenTK.Windowing.GraphicsLibraryFramework;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;
using Visualizer.App.Visualizers;

if (args.Contains("--ui")) { UiChecks.Run(); return; }
using var window = new NativeWindow(new NativeWindowSettings
{
    ClientSize = new Vector2i(640, 640), StartVisible = false,
    API = ContextAPI.OpenGL, APIVersion = new Version(3, 3), Profile = ContextProfile.Core
});
window.Context.MakeCurrent();
GL.LoadBindings(new GLFWBindingsContext());
var failures = new List<string>();
using var engine = new RenderEngine(message =>
{
    Console.WriteLine(message);
    if (message.StartsWith("Rendering failure")) failures.Add(message);
});
VisualizerBase[] modes = [new OscilloscopeVisualizer(), new CircularOscilloscopeVisualizer(),
    new CircularOscilloscopeVisualizer(3), new CircularOscilloscopeVisualizer(4),
    new CircularOscilloscopeVisualizer(5), new CircularOscilloscopeVisualizer(6),
    new CircularOscilloscopeVisualizer(8), new CircularOscilloscopeVisualizer(5, true),
    new SpectrumVisualizer(), new RadialSpectrumVisualizer()];
foreach (var mode in modes)
{
    engine.SetVisualizer(mode);
    foreach (var (width, height) in new[] { (640, 360), (360, 640), (640, 640) })
    foreach (var threeD in new[] { false, true })
    foreach (var angle in new[] { 0f, 45f, 89f, 135f, 180f, 260f })
    {
        engine.ThreeDEnabled = threeD;
        engine.RotationDegrees = angle;
        engine.TiltDegrees = angle * 0.7f;
        engine.TurnDegrees = angle;
        engine.Render(AudioFeatures.Silence, 1f / 60, width, height);
        var error = GL.GetError();
        if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError) throw new Exception($"{mode.Name}: {error}");
        var pixels = new byte[width * height * 4];
        GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        if (!Enumerable.Range(0, width * height).Any(i => pixels[i * 4 + 1] > 50))
            throw new Exception($"No visible pixels: {mode.Name}, {width}x{height}, 3D={threeD}, angle={angle}");
    }
    Console.WriteLine($"PASS: {mode.Name} renders in 2D/3D at landscape, portrait, and square sizes");
}
// Verify each overlay changes the image, animates, combines, and can be disabled
// without leaving GL state behind. Read real GPU output, not shader source text.
foreach (var mode in modes)
{
    engine.SetVisualizer(mode);
    foreach (var (width, height) in new[] { (640, 360), (360, 640) })
    foreach (var threeD in new[] { false, true })
    {
        engine.ThreeDEnabled = threeD;
        engine.RotationDegrees = 25;
        engine.TiltDegrees = 35;
        engine.TurnDegrees = 40;
        engine.SnowIntensity = engine.InterlaceIntensity = 0;
        var baseline = Capture(width, height);
        foreach (var (snow, scan) in new[] { (0.3f, 0f), (0f, 0.5f), (0.3f, 0.5f), (1f, 1f) })
        {
            engine.SnowIntensity = snow;
            engine.InterlaceIntensity = scan;
            var first = Capture(width, height);
            var second = Capture(width, height);
            if (baseline.SequenceEqual(first)) throw new Exception($"CRT effect missing: {mode.Name}, {snow}/{scan}");
            if (first.SequenceEqual(second)) throw new Exception($"CRT animation frozen: {mode.Name}, {snow}/{scan}");
        }
        engine.SnowIntensity = engine.InterlaceIntensity = 0;
        if (!baseline.SequenceEqual(Capture(width, height)))
            throw new Exception($"Disabling CRT effects did not restore {mode.Name}");
    }
    Console.WriteLine($"PASS: {mode.Name} CRT snow, scan, combined, animation and off in 2D/3D and resized viewports");
}

byte[] Capture(int width, int height)
{
    engine.Render(AudioFeatures.Silence, 1f / 60, width, height);
    var pixels = new byte[width * height * 4];
    GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
    var error = GL.GetError();
    if (error != OpenTK.Graphics.OpenGL4.ErrorCode.NoError) throw new Exception($"CRT GL error: {error}");
    return pixels;
}


// A uniform changing input must move every outline vertex equally, including
// the former fixed join. Read back the actual geometry uploaded to OpenGL.
engine.SnowIntensity = engine.InterlaceIntensity = 0;
foreach (var shape in modes.OfType<CircularOscilloscopeVisualizer>())
{
    engine.SetVisualizer(shape);
    var positive = ReadOutline(0.1f);
    var negative = ReadOutline(-0.1f);
    var corners = shape.Name switch
    {
        "triangle oscilloscope" => 3, "square oscilloscope" => 4,
        "pentagon oscilloscope" => 5, "hexagon oscilloscope" => 6,
        "octagon oscilloscope" => 8, "star oscilloscope" => 10, _ => 0
    };
    var count = corners == 0 ? 1024 : 1024 - 1024 % corners;
    for (var i = 0; i < count; i++)
    {
        var outer = MathF.Sqrt(positive[2 * i] * positive[2 * i] + positive[2 * i + 1] * positive[2 * i + 1]);
        var inner = MathF.Sqrt(negative[2 * i] * negative[2 * i] + negative[2 * i + 1] * negative[2 * i + 1]);
        if (MathF.Abs(outer - inner - 0.13f) > 0.0001f)
            throw new Exception($"Pinned or attenuated outline vertex: {shape.Name}, {i}");
    }
    Console.WriteLine($"PASS: {shape.Name} responds at full strength around its entire outline");

    float[] ReadOutline(float value)
    {
        engine.Render(new AudioFeatures { Waveform = Enumerable.Repeat(value, 1024).ToArray() }, 1f / 60, 640, 640);
        GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize, out int bytes);
        var vertices = new float[bytes / sizeof(float)];
        GL.GetBufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero, bytes, vertices);
        return vertices;
    }
}

// A white test image makes the moving bands measurable. Average each four-row
// group to cancel the alternating two-pixel interlace lines.
using (var shaders = new ShaderManager())
using (var effects = new CrtEffects())
{
    GL.Viewport(0, 0, 640, 600);
    GL.ClearColor(1, 1, 1, 1);
    var first = BandCenters(0);
    var second = BandCenters(0.1f);
    if (first.Count != 3 || second.Count != 3) throw new Exception("Expected exactly three scan bands");
    for (var i = 0; i < 3; i++)
        if (second[i] >= first[i]) throw new Exception("Scan bands must move downward");
    GL.ClearColor(0, 0, 0, 1);
    GL.Clear(ClearBufferMask.ColorBufferBit);
    effects.Render(shaders, 0, 600, 0, 0.5f);
    var blackScan = new byte[600 * 4];
    GL.ReadPixels(320, 0, 1, 600, PixelFormat.Rgba, PixelType.UnsignedByte, blackScan);
    if (Enumerable.Range(0, 600).Max(y => blackScan[y * 4 + 1]) < 8)
        throw new Exception("Interlace must be visible on black with snow disabled");
    Console.WriteLine("PASS: interlace visible without snow on black at default intensity");
    GL.ClearColor(0.018f, 0.02f, 0.026f, 1f);
    Console.WriteLine("PASS: exactly three interlace bands moving downward");

    List<float> BandCenters(float delta)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit);
        effects.Render(shaders, delta, 600, 0, 1);
        var pixels = new byte[600 * 4];
        GL.ReadPixels(320, 0, 1, 600, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        var centers = new List<float>();
        var start = -1;
        for (var y = 0; y <= 600; y += 4)
        {
            var dark = y < 600 && Enumerable.Range(y, 4).Average(row => (double)pixels[row * 4]) < 160;
            if (dark && start < 0) start = y;
            if (!dark && start >= 0) { centers.Add((start + y) / 2f); start = -1; }
        }
        return centers;
    }
}
if (GL.GetError() != OpenTK.Graphics.OpenGL4.ErrorCode.NoError) throw new Exception("Geometry or scan-band GL error");
foreach (var mode in modes) mode.Dispose();
if (failures.Count > 0) throw new Exception(string.Join(Environment.NewLine, failures));
