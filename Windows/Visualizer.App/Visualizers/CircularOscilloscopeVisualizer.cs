using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public sealed class CircularOscilloscopeVisualizer : VisualizerBase
{
    private readonly float[] _ring = new float[1024 * 2];
    private AudioFeatures _audio = AudioFeatures.Silence;
    private readonly int _sides;
    private readonly bool _star;
    public CircularOscilloscopeVisualizer(int sides = 0, bool star = false)
    {
        if (sides != 0 && (sides < 3 || sides > 32)) throw new ArgumentOutOfRangeException(nameof(sides));
        if (star && sides == 0) throw new ArgumentException("A star needs at least three points.", nameof(sides));
        _sides = sides;
        _star = star;
    }
    public override string Name => _star ? "star oscilloscope" : _sides switch { 0 => "circular oscilloscope", 3 => "triangle oscilloscope", 4 => "square oscilloscope", 5 => "pentagon oscilloscope", 6 => "hexagon oscilloscope", 8 => "octagon oscilloscope", _ => $"{_sides}-sided oscilloscope" };

    public override void Update(AudioFeatures audio, float deltaTime) => _audio = audio;

    public override void Render(RenderContext context)
    {
        var count = Math.Min(_audio.Waveform.Length, _ring.Length / 2);
        var corners = _star ? _sides * 2 : _sides;
        if (corners > 0) count -= count % corners;
        if (count < 3) return;
        // Use the shorter framebuffer dimension for both axes to keep a true
        // circle in wide windows, portrait windows, and fullscreen.
        var size = Math.Min(context.Width, context.Height);
        var xScale = size / (float)context.Width;
        var yScale = size / (float)context.Height;
        for (var i = 0; i < count; i++)
        {
            var angle = MathF.Tau * i / count + MathF.PI / 2;
            // Traverse the audio window forwards and backwards to close the loop.
            // Unlike tapering, this preserves full displacement at every angle.
            var progress = 2f * i / count;
            var samplePosition = (progress <= 1f ? progress : 2f - progress) * (_audio.Waveform.Length - 1);
            var sampleIndex = (int)samplePosition;
            var nextIndex = Math.Min(sampleIndex + 1, _audio.Waveform.Length - 1);
            var sample = _audio.Waveform[sampleIndex] +
                (_audio.Waveform[nextIndex] - _audio.Waveform[sampleIndex]) * (samplePosition - sampleIndex);
            var baseRadius = 0.55f;
            if (_star)
            {
                var sector = MathF.Tau / corners;
                var position = (float)i * corners / count;
                var edge = (int)position;
                var phase = (position - edge) * sector;
                var start = edge % 2 == 0 ? 0.72f : 0.34f;
                var end = edge % 2 == 0 ? 0.34f : 0.72f;
                baseRadius = start * end * MathF.Sin(sector) / (end * MathF.Sin(sector - phase) + start * MathF.Sin(phase));
            }
            else if (_sides > 0)
            {
                var sector = MathF.Tau / _sides;
                var phase = (MathF.Tau * i / count + (_sides == 4 ? sector / 2 : 0)) % sector;
                baseRadius = 0.68f * MathF.Cos(MathF.PI / _sides) / MathF.Cos(phase - sector / 2);
            }
            var radius = Math.Clamp(baseRadius + sample * 0.65f, 0.12f, 0.88f);
            _ring[i * 2] = MathF.Cos(angle) * radius * xScale;
            _ring[i * 2 + 1] = MathF.Sin(angle) * radius * yScale;
        }
        GL.LineWidth(2.2f);
        DrawPrimary(_ring, count * 2, PrimitiveType.LineLoop);
    }
}
