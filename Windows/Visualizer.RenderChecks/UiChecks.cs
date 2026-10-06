using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Visualizer.App;

internal static class UiChecks
{
    public static void Run()
    {
        // This test runs the WPF/GL UI exclusively on its dedicated STA thread.
        OpenTK.Windowing.Desktop.GLFWProvider.CheckForMainThread = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var window = new MainWindow();
                var panel = (Grid)window.FindName("ControlsPanel");
                var rotation = (ComboBox)window.FindName("RotationModeSelector");
                var crt = (ComboBox)window.FindName("CrtModeSelector");
                foreach (var width in new[] { 1032d, 1272d })
                {
                    for (var mode = 0; mode < 3; mode++)
                    {
                        rotation.SelectedIndex = mode;
                        CheckEnabled("RotationSlider", mode > 0);
                        CheckEnabled("TiltSlider", mode == 2);
                        CheckEnabled("TurnSlider", mode == 2);
                    }
                    for (var mode = 0; mode < 4; mode++)
                    {
                        crt.SelectedIndex = mode;
                        CheckEnabled("SnowSlider", mode is 1 or 3);
                        CheckEnabled("InterlaceSlider", mode is 2 or 3);
                    }
                    panel.Measure(new Size(width, 258));
                    panel.Arrange(new Rect(0, 0, width, 258));
                    panel.UpdateLayout();
                    foreach (var name in new[] { "RotationModeSelector", "CrtModeSelector", "RotationSlider", "TiltSlider", "TurnSlider", "SnowSlider", "InterlaceSlider" })
                    {
                        var control = (FrameworkElement)window.FindName(name);
                        var bounds = control.TransformToAncestor(panel).TransformBounds(new Rect(control.RenderSize));
                        if (bounds.Left < 0 || bounds.Right > width || bounds.Bottom > 258 || bounds.Width < 50)
                            throw new Exception($"Clipped control: {name} at width {width}: {bounds}");
                    }
                    var bitmap = new RenderTargetBitmap((int)width, 258, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(panel);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"visualizer-controls-{width}.png");
                    using var file = System.IO.File.Create(path);
                    encoder.Save(file);
                    Console.WriteLine($"PASS: mode selections and control layout at {width}px; preview: {path}");
                }
                window.Close();
                void CheckEnabled(string name, bool expected)
                {
                    if (((UIElement)window.FindName(name)).IsEnabled != expected)
                        throw new Exception($"Incorrect enabled state: {name}");
                }
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
    }
}
