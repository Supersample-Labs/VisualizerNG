using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public sealed class RadialSpectrumVisualizer : VisualizerBase
{
    private readonly float[] _levels = new float[64];
    private readonly float[] _rays = new float[64 * 4];
    private readonly float[] _ring = new float[128 * 2];
    public override string Name => "radial spectrum";
    public RadialSpectrumVisualizer() => Palette = VisualizerPalette.All[2];

    public override void Update(AudioFeatures audio, float deltaTime)
    {
        for (var i = 0; i < _levels.Length; i++)
        {
            var target = i < audio.Spectrum.Length ? MathF.Sqrt(Math.Clamp(audio.Spectrum[i], 0, 1)) : 0;
            var speed = target > _levels[i] ? 24f : 8f;
            _levels[i] += (target - _levels[i]) * (1f - MathF.Exp(-speed * deltaTime));
        }
    }

    public override void Render(RenderContext context)
    {
        var size = Math.Min(context.Width, context.Height);
        var xScale = size / (float)context.Width;
        var yScale = size / (float)context.Height;
        for (var i = 0; i < _levels.Length; i++)
        {
            var angle = MathF.Tau * i / _levels.Length - MathF.PI / 2;
            var x = MathF.Cos(angle) * xScale;
            var y = MathF.Sin(angle) * yScale;
            var radius = 0.36f + Math.Max(0.015f, _levels[i] * 0.5f);
            _rays[i * 4] = x * 0.36f;
            _rays[i * 4 + 1] = y * 0.36f;
            _rays[i * 4 + 2] = x * radius;
            _rays[i * 4 + 3] = y * radius;
        }
        for (var i = 0; i < _ring.Length / 2; i++)
        {
            var angle = MathF.Tau * i / (_ring.Length / 2);
            _ring[i * 2] = MathF.Cos(angle) * 0.35f * xScale;
            _ring[i * 2 + 1] = MathF.Sin(angle) * 0.35f * yScale;
        }
        GL.LineWidth(3f);
        DrawPrimary(_rays, _rays.Length, PrimitiveType.Lines);
        GL.LineWidth(2f);
        DrawAccent(_ring, _ring.Length, PrimitiveType.LineLoop, 0.7f);
    }

    public override void Reset() => Array.Clear(_levels);
}
