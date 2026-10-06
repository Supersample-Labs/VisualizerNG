using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public sealed class OscilloscopeVisualizer : VisualizerBase
{
    private readonly float[] _line = new float[1024 * 2];
    private AudioFeatures _audio = AudioFeatures.Silence;

    public override string Name => "oscilloscope";
    public float Scale { get; set; } = 1.6f;

    public override void Update(AudioFeatures audio, float deltaTime)
    {
        _audio = audio;
    }

    public override void Render(RenderContext context)
    {
        var waveform = _audio.Waveform;
        if (waveform.Length == 0)
        {
            return;
        }

        GL.LineWidth(2.2f);

        for (var i = 0; i < waveform.Length; i++)
        {
            var x = -1f + 2f * i / (waveform.Length - 1);
            var y = Math.Clamp(waveform[i] * Scale, -0.86f, 0.86f);
            _line[i * 2] = x;
            _line[i * 2 + 1] = y;
        }

        DrawPrimary(_line, waveform.Length * 2, PrimitiveType.LineStrip);
    }
}
