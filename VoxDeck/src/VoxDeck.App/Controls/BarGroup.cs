using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace VoxDeck.App.Controls;

// Rounded accent bars that can bounce like an equalizer. LogoMark and EqBars set the bar shapes.
public abstract class BarGroup : Canvas
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(BarGroup), new PropertyMetadata(false, (d, _) => ((BarGroup)d).UpdateBounce()));

    readonly List<ScaleTransform> _scales = [];

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    protected void AddBar(double x, double width, double height, double canvasHeight, double originY)
    {
        var scale = new ScaleTransform();
        var bar = new Rectangle
        {
            Width = width,
            Height = height,
            RadiusX = width / 2,
            RadiusY = width / 2,
            RenderTransform = scale,
            RenderTransformOrigin = new Point(0.5, originY),
        };
        bar.SetResourceReference(Shape.FillProperty, "Accent");
        SetLeft(bar, x);
        SetTop(bar, originY >= 1 ? canvasHeight - height : (canvasHeight - height) / 2);
        Children.Add(bar);
        _scales.Add(scale);
    }

    /// <summary>The logo "drops in": each bar overshoots and settles, one after another.</summary>
    public void Play()
    {
        if (!Motion.Enabled) return;
        for (var i = 0; i < _scales.Count; i++)
        {
            var grow = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(1050), BeginTime = TimeSpan.FromMilliseconds(i * 70) };
            foreach (var (percent, value) in new[] { (0.0, 0.0), (0.3, 1.25), (0.5, 0.62), (0.68, 1.12), (0.84, 0.95), (1.0, 1.0) })
                grow.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromPercent(percent), Motion.EaseOut));
            _scales[i].ScaleY = 0;
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
    }

    void UpdateBounce()
    {
        double[] durations = [0.5, 0.38, 0.58, 0.44];
        for (var i = 0; i < _scales.Count; i++)
        {
            if (!IsActive || !Motion.Enabled)
            {
                _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, null);
                continue;
            }
            _scales[i].BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.3, 1, TimeSpan.FromSeconds(durations[i % durations.Length]))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(i * 100),
                EasingFunction = Motion.Ease,
            });
        }
    }
}

/// <summary>VoxDeck's mark: four rounded bars. Size it with Width/Height (designed at 20×20).</summary>
public sealed class LogoMark : Viewbox
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(LogoMark), new PropertyMetadata(false, (d, e) => ((LogoMark)d)._bars.IsActive = (bool)e.NewValue));

    readonly Bars _bars = new();

    public LogoMark() => Child = _bars;

    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    public void Play() => _bars.Play();

    sealed class Bars : BarGroup
    {
        public Bars()
        {
            Width = 20;
            Height = 20;
            double[] heights = [9, 15, 19, 12];
            for (var i = 0; i < heights.Length; i++) AddBar(1 + i * 4.8, 3.2, heights[i], 20, 0.5);
        }
    }
}

/// <summary>Three small bars shown next to a playing sound's name.</summary>
public sealed class EqBars : BarGroup
{
    public EqBars()
    {
        Width = 12;
        Height = 12;
        for (var i = 0; i < 3; i++) AddBar(i * 4.5, 3, 12, 12, 1);
    }
}
