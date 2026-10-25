using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VoxDeck.App.Controls;

// Peak bars for a sound. Left click/drag seeks, right click sets the start point, arrow keys seek by 5 %.
// Commands receive the position as a 0..1 fraction.
public sealed class WaveformView : FrameworkElement
{
    public static readonly DependencyProperty PeaksProperty = DependencyProperty.Register(
        nameof(Peaks), typeof(float[]), typeof(WaveformView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        nameof(Progress), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StartMarkerProperty = DependencyProperty.Register(
        nameof(StartMarker), typeof(double), typeof(WaveformView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SeekCommandProperty = DependencyProperty.Register(
        nameof(SeekCommand), typeof(ICommand), typeof(WaveformView));

    public static readonly DependencyProperty SetStartCommandProperty = DependencyProperty.Register(
        nameof(SetStartCommand), typeof(ICommand), typeof(WaveformView));

    public WaveformView()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
    }

    public float[]? Peaks { get => (float[]?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public double StartMarker { get => (double)GetValue(StartMarkerProperty); set => SetValue(StartMarkerProperty, value); }
    public ICommand? SeekCommand { get => (ICommand?)GetValue(SeekCommandProperty); set => SetValue(SeekCommandProperty, value); }
    public ICommand? SetStartCommand { get => (ICommand?)GetValue(SetStartCommandProperty); set => SetValue(SetStartCommandProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var played = (Brush)FindResource("Accent");
        var idle = (Brush)FindResource("WaveIdle");
        dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height)); // makes the whole area clickable

        var peaks = Peaks;
        if (peaks is null || peaks.Length == 0)
        {
            dc.DrawRectangle(idle, null, new Rect(0, height / 2 - 1, width, 2));
            dc.DrawRectangle(played, null, new Rect(0, height / 2 - 1, width * Progress, 2));
        }
        else
        {
            var step = width / peaks.Length;
            var barWidth = Math.Max(1, step - 1.5);
            for (var i = 0; i < peaks.Length; i++)
            {
                var barHeight = Math.Max(2, peaks[i] * height);
                var x = i * step;
                dc.DrawRoundedRectangle(x / width < Progress ? played : idle, null,
                    new Rect(x, (height - barHeight) / 2, barWidth, barHeight), 1, 1);
            }
        }

        if (StartMarker > 0)
        {
            var text = (Brush)FindResource("Text");
            var x = StartMarker * width;
            dc.DrawRectangle(text, null, new Rect(x - 1, 0, 2, height));
            dc.DrawEllipse(text, null, new Point(x, 0), 4, 4);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        CaptureMouse();
        Run(SeekCommand, Fraction(e));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) Run(SeekCommand, Fraction(e));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) => ReleaseMouseCapture();

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        Run(SetStartCommand, Fraction(e));
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var delta = e.Key switch { Key.Left => -0.05, Key.Right => 0.05, _ => 0.0 };
        if (delta == 0) return;
        Run(SeekCommand, Math.Clamp(Progress + delta, 0, 1));
        e.Handled = true;
    }

    double Fraction(MouseEventArgs e) => ActualWidth > 0 ? Math.Clamp(e.GetPosition(this).X / ActualWidth, 0, 1) : 0;

    static void Run(ICommand? command, double fraction)
    {
        if (command?.CanExecute(fraction) == true) command.Execute(fraction);
    }
}
