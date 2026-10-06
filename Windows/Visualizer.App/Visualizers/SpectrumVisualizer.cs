using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public sealed class SpectrumVisualizer : VisualizerBase
{
    private const int MaxBars = 64;
    private readonly float[] _bars = new float[MaxBars];
    private readonly float[] _peaks = new float[MaxBars];
    private readonly float[] _barVertices = new float[MaxBars * 6 * 2];
    private readonly float[] _peakVertices = new float[MaxBars * 2 * 2];
    private AudioFeatures _audio = AudioFeatures.Silence;

    public override string Name => "spectrum";
    public SpectrumVisualizer() => Palette = VisualizerPalette.All[1];

    public override void Update(AudioFeatures audio, float deltaTime)
    {
        _audio = audio;
        var spectrum = audio.Spectrum;
        var count = Math.Min(MaxBars, spectrum.Length);
        for (var i = 0; i < count; i++)
        {
            var target = spectrum[i];
            var attack = 1f - MathF.Pow(0.001f, deltaTime * 14f);
            var release = 1f - MathF.Pow(0.001f, deltaTime * 5f);
            _bars[i] += (target - _bars[i]) * (target > _bars[i] ? attack : release);

            if (_bars[i] >= _peaks[i])
            {
                _peaks[i] = _bars[i];
            }
            else
            {
                _peaks[i] = MathF.Max(0f, _peaks[i] - deltaTime * 0.28f);
            }
        }
    }

    public override void Render(RenderContext context)
    {
        var count = Math.Min(MaxBars, _audio.Spectrum.Length);
        if (count == 0)
        {
            return;
        }

        var baseY = -0.86f;
        var maxHeight = 1.62f;
        var slot = 2f / count;
        var width = slot * 0.72f;
        var vertex = 0;

        for (var i = 0; i < count; i++)
        {
            var left = -1f + slot * i + (slot - width) * 0.5f;
            var right = left + width;
            var top = baseY + MathF.Max(0.018f, _bars[i] * maxHeight);
            WriteQuad(_barVertices, ref vertex, left, baseY, right, top);
        }

        DrawPrimary(_barVertices, vertex, PrimitiveType.Triangles, 0.88f);

        var peakVertex = 0;
        for (var i = 0; i < count; i++)
        {
            var left = -1f + slot * i + (slot - width) * 0.5f;
            var right = left + width;
            var y = baseY + MathF.Max(0.025f, _peaks[i] * maxHeight);
            _peakVertices[peakVertex++] = left;
            _peakVertices[peakVertex++] = y;
            _peakVertices[peakVertex++] = right;
            _peakVertices[peakVertex++] = y;
        }

        GL.LineWidth(2f);
        DrawAccent(_peakVertices, peakVertex, PrimitiveType.Lines);
    }

    public override void Reset()
    {
        Array.Clear(_bars);
        Array.Clear(_peaks);
    }

    private static void WriteQuad(float[] vertices, ref int index, float left, float bottom, float right, float top)
    {
        vertices[index++] = left;
        vertices[index++] = bottom;
        vertices[index++] = right;
        vertices[index++] = bottom;
        vertices[index++] = right;
        vertices[index++] = top;
        vertices[index++] = left;
        vertices[index++] = bottom;
        vertices[index++] = right;
        vertices[index++] = top;
        vertices[index++] = left;
        vertices[index++] = top;
    }
}
