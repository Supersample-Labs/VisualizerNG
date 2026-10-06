using Visualizer.App.Audio;
using Visualizer.App.Rendering;

namespace Visualizer.App.Visualizers;

public interface IVisualizer : IDisposable
{
    string Name { get; }
    VisualizerPalette Palette { get; set; }
    void Initialize(ShaderManager shaders);
    void Update(AudioFeatures audio, float deltaTime);
    void Render(RenderContext context);
    void Reset();
}
