using System.Windows;
using System.Windows.Controls;

namespace Visualizer.App.UI;

public partial class MeterControl : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(MeterControl), new PropertyMetadata("", OnLabelChanged));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(float), typeof(MeterControl), new PropertyMetadata(0f, OnValueChanged));

    public MeterControl()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateFill();
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public float Value
    {
        get => (float)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MeterControl)d).LabelText.Text = (string)e.NewValue;
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((MeterControl)d).UpdateFill();
    }

    private void UpdateFill()
    {
        var value = Math.Clamp(Value, 0f, 1f);
        Fill.Width = ActualWidth * value;
        ValueText.Text = $"{value * 100f:0}%";
    }
}
