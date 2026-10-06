using NAudio.Dsp;

namespace Visualizer.App.Audio;

public sealed class FFTProcessor
{
    private readonly int _fftSize;
    private readonly int _m;
    private readonly Complex[] _fftBuffer;
    private readonly float[] _window;
    private readonly float[] _spectrum;

    public FFTProcessor(int fftSize)
    {
        if ((fftSize & (fftSize - 1)) != 0)
        {
            throw new ArgumentException("FFT size must be a power of two.", nameof(fftSize));
        }

        _fftSize = fftSize;
        _m = (int)Math.Log2(fftSize);
        _fftBuffer = new Complex[fftSize];
        _window = new float[fftSize];
        _spectrum = new float[fftSize / 2];

        for (var i = 0; i < fftSize; i++)
        {
            _window[i] = (float)(0.5 - 0.5 * Math.Cos(2.0 * Math.PI * i / (fftSize - 1)));
        }
    }

    public float[] Process(ReadOnlySpan<float> samples)
    {
        var available = Math.Min(_fftSize, samples.Length);
        var start = samples.Length - available;
        var pad = _fftSize - available;

        for (var i = 0; i < _fftSize; i++)
        {
            var sourceIndex = i - pad;
            var sample = sourceIndex >= 0 ? samples[start + sourceIndex] : 0f;
            _fftBuffer[i].X = sample * _window[i];
            _fftBuffer[i].Y = 0f;
        }

        FastFourierTransform.FFT(true, _m, _fftBuffer);

        for (var i = 0; i < _spectrum.Length; i++)
        {
            var real = _fftBuffer[i].X;
            var imaginary = _fftBuffer[i].Y;
            var magnitude = MathF.Sqrt(real * real + imaginary * imaginary);
            _spectrum[i] = MathF.Min(1f, MathF.Log10(1f + magnitude * 18f));
        }

        return _spectrum;
    }
}
