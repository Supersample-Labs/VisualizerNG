namespace Visualizer.App.Audio;

public sealed class AudioFeatures
{
    public static readonly AudioFeatures Silence = new()
    {
        Spectrum = new float[64],
        Waveform = new float[1024]
    };

    public float Amplitude { get; init; }
    public float Peak { get; init; }
    public float Bass { get; init; }
    public float Mid { get; init; }
    public float Treble { get; init; }
    public float[] Spectrum { get; init; } = [];
    public float[] Waveform { get; init; } = [];
}
