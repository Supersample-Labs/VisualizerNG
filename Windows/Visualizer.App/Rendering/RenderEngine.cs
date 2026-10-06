using OpenTK.Graphics.OpenGL4;
using Visualizer.App.Audio;
using Visualizer.App.Visualizers;

namespace Visualizer.App.Rendering;

public sealed class RenderEngine : IDisposable
{
    private readonly Action<string> _log;
    private readonly ShaderManager _shaders = new();
    private readonly CrtEffects _crtEffects = new();
    public float SnowIntensity { get; set; }
    public float InterlaceIntensity { get; set; }
    private IVisualizer? _visualizer;
    public float RotationDegrees { get; set; }
    public bool ThreeDEnabled { get; set; }
    public float TiltDegrees { get; set; }
    public float TurnDegrees { get; set; }
    private bool _initialized;
    private bool _visualizerChanged;

    public RenderEngine(Action<string> log)
    {
        _log = log;
    }

    public void SetVisualizer(IVisualizer visualizer)
    {
        _visualizer = visualizer;
        _visualizerChanged = true;
    }

    public void Render(AudioFeatures audio, float deltaTime, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        try
        {
            if (!_initialized)
            {
                GL.ClearColor(0.018f, 0.02f, 0.026f, 1f);
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
                _initialized = true;
                _log($"OpenGL initialized: {GL.GetString(StringName.Version)}");
            }

            // OpenGL resources must be created inside the render callback,
            // where GLWpfControl guarantees that its context is current.
            if (_visualizerChanged && _visualizer is not null)
            {
                _visualizer.Initialize(_shaders);
                _visualizer.Reset();
                _visualizerChanged = false;
            }

            GL.Viewport(0, 0, width, height);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            _visualizer?.Update(audio, deltaTime);
            if (_visualizer is VisualizerBase drawable) drawable.SetRotation(RotationDegrees, width, height, ThreeDEnabled, TiltDegrees, TurnDegrees);
            _visualizer?.Render(new RenderContext(width, height));
            _crtEffects.Render(_shaders, deltaTime, height, SnowIntensity, InterlaceIntensity);
        }
        catch (Exception ex)
        {
            _log($"Rendering failure: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _crtEffects.Dispose();
        _shaders.Dispose();
    }
}
