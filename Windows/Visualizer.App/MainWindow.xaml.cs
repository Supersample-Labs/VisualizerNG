using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenTK.Wpf;
using Visualizer.App.Audio;
using Visualizer.App.Rendering;
using Visualizer.App.Visualizers;

namespace Visualizer.App;

public partial class MainWindow : Window
{
    private readonly AudioRingBuffer _audioBuffer = new(48000 * 8);
    private readonly AudioCaptureService _capture;
    private readonly AudioAnalyzer _analyzer;
    private readonly RenderEngine _renderEngine;
    private readonly List<IVisualizer> _visualizers;
    private readonly DispatcherTimer _deviceRefreshTimer;
    private readonly DispatcherTimer _cursorTimer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastRenderTime;
    private float _spinAngle;
    private float _tiltSpinAngle;
    private float _turnSpinAngle;
    private bool _isLoadingDevices;
    private bool _isFullscreen;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode;

    public MainWindow()
    {
        InitializeComponent();
        UpdateEffectControls();
        Title = typeof(MainWindow).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyProductAttribute), false)
            .OfType<System.Reflection.AssemblyProductAttribute>().FirstOrDefault()?.Product ?? Title;

        _capture = new AudioCaptureService(_audioBuffer, Log);
        _analyzer = new AudioAnalyzer(_audioBuffer);
        _renderEngine = new RenderEngine(Log);
        _visualizers =
        [
            new OscilloscopeVisualizer(),
            new CircularOscilloscopeVisualizer(),
            new CircularOscilloscopeVisualizer(3),
            new CircularOscilloscopeVisualizer(4),
            new CircularOscilloscopeVisualizer(5),
            new CircularOscilloscopeVisualizer(6),
            new CircularOscilloscopeVisualizer(8),
            new CircularOscilloscopeVisualizer(5, star: true),
            new SpectrumVisualizer(),
            new RadialSpectrumVisualizer()
        ];

        Loaded += OnLoaded;
        MouseMove += OnMouseActivity;

        _deviceRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _deviceRefreshTimer.Tick += (_, _) =>
        {
            RefreshDevices(false);
            if (_capture.IsWaitingForAudio)
                Log("No audio detected. Select the speakers/headphones playing your audio.");
            else if (_capture.HasRecentSignal && _capture.IsCapturing)
                Log("Receiving audio: " + _capture.GetOutputDevices().FirstOrDefault(d => d.Id == _capture.CurrentDeviceId)?.Name);
        };

        _cursorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _cursorTimer.Tick += (_, _) =>
        {
            if (_isFullscreen)
            {
                Cursor = Cursors.None;
            }
        };

        // Start before the control becomes visible so GLWpfControl subscribes
        // to WPF's composition clock on its first IsVisibleChanged event.
        GlView.Start(new GLWpfControlSettings
        {
            MajorVersion = 3,
            MinorVersion = 3,
            RenderContinuously = true
        });
        GlView.Render += OnGlRender;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ColorSelector.ItemsSource = VisualizerPalette.All;
        VisualizerSelector.ItemsSource = _visualizers;
        VisualizerSelector.DisplayMemberPath = nameof(IVisualizer.Name);
        VisualizerSelector.SelectedIndex = 0;
        _renderEngine.SetVisualizer(_visualizers[0]);

        RefreshDevices(true);
        if (DeviceSelector.SelectedItem is AudioDeviceInfo device)
        {
            _capture.Start(string.IsNullOrEmpty(device.Id) ? null : device.Id);
        }
        else
        {
            _capture.Start();
        }

        _deviceRefreshTimer.Start();
    }

    private void RefreshDevices(bool selectDefault)
    {
        _isLoadingDevices = true;
        var previousId = (DeviceSelector.SelectedItem as AudioDeviceInfo)?.Id ?? _capture.CurrentDeviceId;
        var devices = _capture.GetOutputDevices().ToList();
        devices.Insert(0, new AudioDeviceInfo("", "Follow Windows default output", false));
        DeviceSelector.ItemsSource = devices;

        var selected = selectDefault
            ? devices[0]
            : devices.FirstOrDefault(device => device.Id == previousId) ?? devices[0];

        DeviceSelector.SelectedItem = selected;
        _isLoadingDevices = false;

        var targetId = string.IsNullOrEmpty(selected.Id)
            ? devices.FirstOrDefault(device => device.IsDefault)?.Id
            : selected.Id;
        if (!selectDefault && (targetId != _capture.CurrentDeviceId || !_capture.IsCapturing))
        {
            _capture.Start(targetId);
        }
    }

    private void OnGlRender(TimeSpan delta)
    {
        var now = _clock.Elapsed;
        var deltaTime = _lastRenderTime == TimeSpan.Zero ? 1f / 60f : (float)(now - _lastRenderTime).TotalSeconds;
        _lastRenderTime = now;

        var audio = _analyzer.Analyze(_capture.SampleRate, (float)SmoothingSlider.Value, (float)SensitivitySlider.Value);
        LevelMeter.Value = audio.Amplitude;
        BassMeter.Value = audio.Bass;
        MidMeter.Value = audio.Mid;
        TrebleMeter.Value = audio.Treble;

        if (RotationModeSelector.SelectedIndex > 0)
            _spinAngle = (_spinAngle + Math.Clamp(deltaTime, 0, 0.1f) * 24f) % 360f;
        if (RotationModeSelector.SelectedIndex == 2)
        {
            _tiltSpinAngle = (_tiltSpinAngle + Math.Clamp(deltaTime, 0, 0.1f) * 17f) % 360f;
            _turnSpinAngle = (_turnSpinAngle + Math.Clamp(deltaTime, 0, 0.1f) * 29f) % 360f;
        }
        _renderEngine.SnowIntensity = CrtModeSelector.SelectedIndex is 1 or 3 ? (float)SnowSlider.Value : 0;
        _renderEngine.InterlaceIntensity = CrtModeSelector.SelectedIndex is 2 or 3 ? (float)InterlaceSlider.Value : 0;
        _renderEngine.ThreeDEnabled = RotationModeSelector.SelectedIndex == 2;
        _renderEngine.TiltDegrees = (float)TiltSlider.Value + _tiltSpinAngle;
        _renderEngine.TurnDegrees = (float)TurnSlider.Value + _turnSpinAngle;
        _renderEngine.RotationDegrees = RotationModeSelector.SelectedIndex > 0 ? (float)RotationSlider.Value + _spinAngle : 0;
        _renderEngine.Render(
            audio,
            Math.Clamp(deltaTime, 0.001f, 0.1f),
            GlView.FrameBufferWidth,
            GlView.FrameBufferHeight);
    }

    private void OnDeviceChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_isLoadingDevices || DeviceSelector.SelectedItem is not AudioDeviceInfo device)
        {
            return;
        }

        _capture.Start(string.IsNullOrEmpty(device.Id) ? null : device.Id);
    }

    private void OnVisualizerChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (VisualizerSelector.SelectedItem is IVisualizer visualizer)
        {
            _renderEngine.SetVisualizer(visualizer);
            ColorSelector.SelectedItem = visualizer.Palette;
        }
    }

    private void OnColorChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (VisualizerSelector.SelectedItem is IVisualizer visualizer && ColorSelector.SelectedItem is VisualizerPalette palette)
            visualizer.Palette = palette;
    }

    private void OnEffectModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateEffectControls();
    }

    private void UpdateEffectControls()
    {
        // SelectionChanged can fire while InitializeComponent is still creating controls.
        if (RotationSlider is null || TiltSlider is null || TurnSlider is null ||
            SnowSlider is null || InterlaceSlider is null || CrtModeSelector is null) return;
        RotationSlider.IsEnabled = RotationModeSelector.SelectedIndex > 0;
        TiltSlider.IsEnabled = TurnSlider.IsEnabled = RotationModeSelector.SelectedIndex == 2;
        SnowSlider.IsEnabled = CrtModeSelector.SelectedIndex is 1 or 3;
        InterlaceSlider.IsEnabled = CrtModeSelector.SelectedIndex is 2 or 3;
    }
    private void OnResetRotation(object sender, RoutedEventArgs e)
    {
        RotationSlider.Value = 0;
        RotationModeSelector.SelectedIndex = 0;
        _spinAngle = 0;
        _tiltSpinAngle = _turnSpinAngle = 0;
        TiltSlider.Value = TurnSlider.Value = 0;

    }

    private void OnFullscreenClick(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void OnAboutClick(object sender, RoutedEventArgs e)
    {
        var tabs = new System.Windows.Controls.TabControl();
        foreach (var item in new[] { ("Privacy", "PRIVACY.txt"), ("Licenses", "THIRD-PARTY-NOTICES.txt") })
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "Legal", item.Item2);
            var text = System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : "Document unavailable in this build.";
            if (item.Item1 == "Licenses")
            {
                var directory = System.IO.Path.Combine(AppContext.BaseDirectory, "Legal", "ThirdParty");
                if (System.IO.Directory.Exists(directory))
                    foreach (var file in System.IO.Directory.EnumerateFiles(directory).Order())
                        text += "\n\n" + System.IO.Path.GetFileName(file) + "\n" + System.IO.File.ReadAllText(file);
            }
            tabs.Items.Add(new System.Windows.Controls.TabItem
            {
                Header = item.Item1,
                Content = new System.Windows.Controls.TextBox
                {
                    Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
                    Padding = new Thickness(16)
                }
            });
        }
        new Window
        {
            Title = $"About {Title} - {typeof(MainWindow).Assembly.GetName().Version}",
            Icon = Icon, Owner = this, Width = 700, Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = tabs
        }.ShowDialog();
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ToggleFullscreen();
            e.Handled = true;
        }
    }

    private void ToggleFullscreen()
    {
        if (!_isFullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            ControlsPanel.Visibility = Visibility.Collapsed;
            VisualizerHost.Margin = new Thickness(0);
            Root.RowDefinitions[1].Height = new GridLength(0);
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Cursor = Cursors.Arrow;
            _cursorTimer.Stop();
            _cursorTimer.Start();
            _isFullscreen = true;
        }
        else
        {
            _cursorTimer.Stop();
            Cursor = Cursors.Arrow;
            ControlsPanel.Visibility = Visibility.Visible;
            VisualizerHost.Margin = new Thickness(16, 16, 16, 8);
            Root.RowDefinitions[1].Height = new GridLength(282);
            WindowStyle = _previousWindowStyle;
            ResizeMode = _previousResizeMode;
            WindowState = _previousWindowState;
            _isFullscreen = false;
        }
    }

    private void OnMouseActivity(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isFullscreen)
        {
            return;
        }

        Cursor = Cursors.Arrow;
        _cursorTimer.Stop();
        _cursorTimer.Start();
    }

    private void Log(string message)
    {
        Dispatcher.InvokeAsync(() => StatusText.Text = $"{DateTime.Now:T}  {message}");
        Trace.WriteLine(message);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _deviceRefreshTimer.Stop();
        _cursorTimer.Stop();
        _capture.Dispose();
        GlView.Render -= OnGlRender;
        GlView.Context?.MakeCurrent();
        _renderEngine.Dispose();
        foreach (var visualizer in _visualizers)
        {
            visualizer.Dispose();
        }
        GlView.Dispose();
    }
}
