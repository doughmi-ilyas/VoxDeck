using System.Windows;
using System.Windows.Media;

namespace VoxDeck.App.Controls;

// Segmented level meter; the top fifth lights up in the warning color.
public sealed class SegMeter : FrameworkElement
{
    const int Segments = 12;

    public static readonly DependencyProperty LevelProperty = Register(nameof(Level), typeof(double), 0.0);
    public static readonly DependencyProperty OnProperty = Register(nameof(On), typeof(Brush), Brushes.SteelBlue);
    public static readonly DependencyProperty OffProperty = Register(nameof(Off), typeof(Brush), Brushes.LightGray);
    public static readonly DependencyProperty HotProperty = Register(nameof(Hot), typeof(Brush), Brushes.Orange);

    static DependencyProperty Register(string name, Type type, object fallback) => DependencyProperty.Register(
        name, type, typeof(SegMeter), new FrameworkPropertyMetadata(fallback, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Level { get => (double)GetValue(LevelProperty); set => SetValue(LevelProperty, value); }
    public Brush On { get => (Brush)GetValue(OnProperty); set => SetValue(OnProperty, value); }
    public Brush Off { get => (Brush)GetValue(OffProperty); set => SetValue(OffProperty, value); }
    public Brush Hot { get => (Brush)GetValue(HotProperty); set => SetValue(HotProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        const double gap = 2;
        var width = (ActualWidth - gap * (Segments - 1)) / Segments;
        var lit = (int)Math.Round(Math.Clamp(Level, 0, 1) * Segments);
        for (var i = 0; i < Segments; i++)
        {
            var brush = i >= lit ? Off : i >= Segments * 0.8 ? Hot : On;
            dc.DrawRoundedRectangle(brush, null, new Rect(i * (width + gap), 0, width, ActualHeight), 1, 1);
        }
    }
}
