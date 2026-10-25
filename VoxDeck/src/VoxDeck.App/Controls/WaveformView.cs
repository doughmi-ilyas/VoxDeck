using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace VoxDeck.App.Controls;

// Peak bars for a sound. Left click/drag seeks, right click sets the start point, arrow keys seek by 5 %.
// Commands receive the position as a 0..1 fraction.
public sealed class WaveformView : FrameworkElement
{
    // IReadOnlyList rather than float[]: the XAML compiler rejects array-typed properties inside templates.
    public static readonly DependencyProperty PeaksProperty = Register(nameof(Peaks), typeof(IReadOnlyList<float>), null);
    public static readonly DependencyProperty ProgressProperty = Register(nameof(Progress), typeof(double), 0.0);
    public static readonly DependencyProperty StartMarkerProperty = Register(nameof(StartMarker), typeof(double), 0.0);
    public static readonly DependencyProperty FillProperty = Register(nameof(Fill), typeof(Brush), Brushes.Gray);
    public static readonly DependencyProperty ProgressFillProperty = Register(nameof(ProgressFill), typeof(Brush), Brushes.SteelBlue);
    public static readonly DependencyProperty MarkerFillProperty = Register(nameof(MarkerFill), typeof(Brush), Brushes.White);

    public static readonly DependencyProperty SeekCommandProperty = DependencyProperty.Register(
        nameof(SeekCommand), typeof(ICommand), typeof(WaveformView));
    public static readonly DependencyProperty SetStartCommandProperty = DependencyProperty.Register(
        nameof(SetStartCommand), typeof(ICommand), typeof(WaveformView));

    static DependencyProperty Register(string name, Type type, object? fallback) => DependencyProperty.Register(
        name, type, typeof(WaveformView), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    public WaveformView()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
    }

    public IReadOnlyList<float>? Peaks { get => (IReadOnlyList<float>?)GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public double StartMarker { get => (double)GetValue(StartMarkerProperty); set => SetValue(StartMarkerProperty, value); }
    public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public Brush ProgressFill { get => (Brush)GetValue(ProgressFillProperty); set => SetValue(ProgressFillProperty, value); }
    public Brush MarkerFill { get => (Brush)GetValue(MarkerFillProperty); set => SetValue(MarkerFillProperty, value); }
    public ICommand? SeekCommand { get => (ICommand?)GetValue(SeekCommandProperty); set => SetValue(SeekCommandProperty, value); }
    public ICommand? SetStartCommand { get => (ICommand?)GetValue(SetStartCommandProperty); set => SetValue(SetStartCommandProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        var size = new Rect(RenderSize);
        dc.DrawRectangle(Brushes.Transparent, null, size); // makes the whole area clickable
        DrawBars(dc, Fill);
        if (Progress > 0)
        {
            // The played part is the same bars in the accent color, clipped at the playhead.
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, size.Width * Math.Clamp(Progress, 0, 1), size.Height)));
            DrawBars(dc, ProgressFill);
            dc.Pop();
        }
        if (StartMarker > 0)
        {
            var x = StartMarker * size.Width;
            dc.DrawRectangle(MarkerFill, null, new Rect(x - 1, 0, 2, size.Height));
            dc.DrawEllipse(MarkerFill, null, new Point(x, 2), 3.5, 3.5);
        }
    }

    void DrawBars(DrawingContext dc, Brush brush)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        var peaks = Peaks;
        if (peaks is null || peaks.Count == 0)
        {
            dc.DrawRoundedRectangle(brush, null, new Rect(0, height / 2 - 1.5, width, 3), 1.5, 1.5);
            return;
        }
        // Bars and gaps share each slot so the waveform always fits, however narrow the control is.
        var slot = width / peaks.Count;
        var gap = Math.Min(2, slot * 0.4);
        for (var i = 0; i < peaks.Count; i++)
        {
            var barHeight = Math.Max(3, peaks[i] * height);
            dc.DrawRoundedRectangle(brush, null, new Rect(i * slot, (height - barHeight) / 2, slot - gap, barHeight), 1, 1);
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (Focusable) Focus();
        CaptureMouse();
        Run(SeekCommand, Fraction(e));
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (IsMouseCaptured) Run(SeekCommand, Fraction(e));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs e)
    {
        if (SetStartCommand is null) return;
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
