using OpenTK.Mathematics;

namespace Visualizer.App.Visualizers;

public sealed record VisualizerPalette(string Name, Vector3 Primary, Vector3 Accent)
{
    public string Swatch => $"#{(byte)(Primary.X * 255):X2}{(byte)(Primary.Y * 255):X2}{(byte)(Primary.Z * 255):X2}";

    public static IReadOnlyList<VisualizerPalette> All { get; } =
    [
        new("Lagoon", new(0.25f, 0.92f, 0.86f), new(0.65f, 1f, 0.95f)),
        new("Electric blue", new(0.35f, 0.64f, 1f), new(1f, 0.86f, 0.32f)),
        new("Neon violet", new(0.74f, 0.38f, 1f), new(1f, 0.48f, 0.83f)),
        new("Sunset", new(1f, 0.43f, 0.24f), new(1f, 0.86f, 0.38f)),
        new("Lime", new(0.55f, 0.95f, 0.25f), new(0.9f, 1f, 0.65f))
    ];
}
