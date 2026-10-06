using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Visualizer.App.Audio;

var buffer = new AudioRingBuffer(48000);
var analyzer = new AudioAnalyzer(buffer);
var samples = Enumerable.Range(0, 4096).Select(i => 0.2f * MathF.Sin(2 * MathF.PI * 440 * i / 48000)).ToArray();
buffer.Write(samples);
var features = analyzer.Analyze(48000, 0, 1);
Check(features.Amplitude > 0.1f && features.Waveform.Any(x => Math.Abs(x) > 0.1f) && features.Spectrum.Max() > 0.01f,
    "Tone reaches waveform, level and spectrum");
buffer.Clear();
Check(analyzer.Analyze(48000, 0, 1).Amplitude == 0, "Clearing capture removes previous audio");

if (args.Contains("--live"))
{
    using var capture = new AudioCaptureService(buffer, Console.WriteLine);
    using var enumerator = new MMDeviceEnumerator();
    using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
    Console.WriteLine($"Testing Windows default output: {device.FriendlyName}");
    capture.Start();
    Check(capture.IsCapturing, "Loopback starts");
    using var playback = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
    playback.Init(new SignalGenerator(48000, 2) { Frequency = 440, Gain = 0.1, Type = SignalGeneratorType.Sin });
    playback.Play();
    var received = false;
    for (var i = 0; i < 20; i++)
    {
        Thread.Sleep(100);
        features = analyzer.Analyze(capture.SampleRate, 0, 1);
        received |= capture.HasRecentSignal && features.Amplitude > 0.001f && features.Spectrum.Max() > 0.001f;
    }
    playback.Stop();
    Check(received, "Real Windows playback reaches loopback, waveform and spectrum");
    capture.Stop();
    Check(!capture.IsCapturing && analyzer.Analyze(48000, 0, 1).Amplitude == 0, "Stopping capture clears audio");
}

static void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}
