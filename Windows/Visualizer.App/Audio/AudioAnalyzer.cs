namespace Visualizer.App.Audio;

public sealed class AudioAnalyzer
{
    private const int AnalysisSamples = 4096;
    private const int WaveformSamples = 1024;
    private const int SpectrumBars = 64;

    private readonly AudioRingBuffer _buffer;
    private readonly FFTProcessor _fft = new(AnalysisSamples);
    private readonly float[] _analysis = new float[AnalysisSamples];
    private readonly float[] _waveform = new float[WaveformSamples];
    private readonly float[] _bars = new float[SpectrumBars];
    private readonly float[] _smoothedBars = new float[SpectrumBars];

    private AudioFeatures _last = AudioFeatures.Silence;
    private float _amplitude;
    private float _peak;
    private float _bass;
    private float _mid;
    private float _treble;

    public AudioAnalyzer(AudioRingBuffer buffer)
    {
        _buffer = buffer;
    }

    public AudioFeatures Analyze(int sampleRate, float smoothing, float sensitivity)
    {
        _buffer.ReadLatest(_analysis);
        DownsampleWaveform();

        var rmsSum = 0f;
        var peak = 0f;
        for (var i = 0; i < _analysis.Length; i++)
        {
            var abs = MathF.Abs(_analysis[i]);
            rmsSum += _analysis[i] * _analysis[i];
            if (abs > peak)
            {
                peak = abs;
            }
        }

        var rms = MathF.Sqrt(rmsSum / _analysis.Length);
        var spectrum = _fft.Process(_analysis);
        BuildLogBars(spectrum, sampleRate);

        var bass = BandLevel(spectrum, sampleRate, 20f, 250f);
        var mid = BandLevel(spectrum, sampleRate, 250f, 2000f);
        var treble = BandLevel(spectrum, sampleRate, 2000f, 20000f);

        var coefficient = Math.Clamp(smoothing, 0f, 0.98f);
        _amplitude = Smooth(_amplitude, Normalize(rms, sensitivity * 5.5f), coefficient);
        _peak = Smooth(_peak, Normalize(peak, sensitivity * 1.8f), coefficient);
        _bass = Smooth(_bass, Normalize(bass, sensitivity * 2.4f), coefficient);
        _mid = Smooth(_mid, Normalize(mid, sensitivity * 2.2f), coefficient);
        _treble = Smooth(_treble, Normalize(treble, sensitivity * 2.8f), coefficient);

        for (var i = 0; i < SpectrumBars; i++)
        {
            _smoothedBars[i] = Smooth(_smoothedBars[i], Normalize(_bars[i], sensitivity * 1.8f), coefficient);
        }

        _last = new AudioFeatures
        {
            Amplitude = _amplitude,
            Peak = _peak,
            Bass = _bass,
            Mid = _mid,
            Treble = _treble,
            Spectrum = (float[])_smoothedBars.Clone(),
            Waveform = (float[])_waveform.Clone()
        };

        return _last;
    }

    public AudioFeatures Last => _last;

    private void DownsampleWaveform()
    {
        var stride = Math.Max(1, _analysis.Length / WaveformSamples);
        for (var i = 0; i < WaveformSamples; i++)
        {
            var source = Math.Min(_analysis.Length - 1, i * stride);
            _waveform[i] = _analysis[source];
        }
    }

    private void BuildLogBars(float[] spectrum, int sampleRate)
    {
        Array.Clear(_bars);
        var nyquist = sampleRate / 2f;
        var minFrequency = 20f;
        var maxFrequency = Math.Min(20000f, nyquist);

        for (var bar = 0; bar < SpectrumBars; bar++)
        {
            var startT = bar / (float)SpectrumBars;
            var endT = (bar + 1) / (float)SpectrumBars;
            var startFreq = LogLerp(minFrequency, maxFrequency, startT);
            var endFreq = LogLerp(minFrequency, maxFrequency, endT);
            _bars[bar] = BandLevel(spectrum, sampleRate, startFreq, endFreq);
        }
    }

    private static float BandLevel(float[] spectrum, int sampleRate, float lowHz, float highHz)
    {
        var binHz = sampleRate / (float)(spectrum.Length * 2);
        var start = Math.Max(1, (int)MathF.Floor(lowHz / binHz));
        var end = Math.Min(spectrum.Length - 1, (int)MathF.Ceiling(highHz / binHz));
        if (end <= start)
        {
            return 0f;
        }

        var sum = 0f;
        var count = 0;
        for (var i = start; i <= end; i++)
        {
            sum += spectrum[i];
            count++;
        }

        return count == 0 ? 0f : sum / count;
    }

    private static float LogLerp(float start, float end, float amount)
    {
        return MathF.Exp(MathF.Log(start) + (MathF.Log(end) - MathF.Log(start)) * amount);
    }

    private static float Smooth(float current, float target, float coefficient)
    {
        return current * coefficient + target * (1f - coefficient);
    }

    private static float Normalize(float value, float gain)
    {
        return Math.Clamp(value * gain, 0f, 1f);
    }
}
