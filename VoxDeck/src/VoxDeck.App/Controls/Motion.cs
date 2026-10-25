using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace VoxDeck.App.Controls;

// Every animation in the app goes through here, so "Reduce motion" (or Windows' own animation setting) turns them all off.
public static class Motion
{
    public static bool UserReduced { get; set; }
    public static bool Enabled => !UserReduced && SystemParameters.ClientAreaAnimation;

    public static readonly IEasingFunction EaseOut = Frozen(new QuinticEase { EasingMode = EasingMode.EaseOut });
    public static readonly IEasingFunction Ease = Frozen(new CubicEase { EasingMode = EasingMode.EaseInOut });
    public static readonly IEasingFunction Spring = Frozen(new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 });

    static IEasingFunction Frozen(EasingFunctionBase ease)
    {
        ease.Freeze();
        return ease;
    }

    public static void Animate(IAnimatable target, DependencyProperty property, double to, int ms,
        IEasingFunction? ease = null, int delay = 0, double? from = null, Action? done = null)
    {
        if (!Enabled || ms == 0)
        {
            target.BeginAnimation(property, null);
            ((DependencyObject)target).SetValue(property, to);
            done?.Invoke();
            return;
        }
        if (from is not null)
        {
            target.BeginAnimation(property, null);
            ((DependencyObject)target).SetValue(property, from.Value); // hold the start value during the delay
        }
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = TimeSpan.FromMilliseconds(ms),
            BeginTime = TimeSpan.FromMilliseconds(delay),
            EasingFunction = ease ?? EaseOut,
        };
        if (done is not null) animation.Completed += (_, _) => done();
        target.BeginAnimation(property, animation);
    }

    /// <summary>Fade in while rising a little: views, drawer sections, toasts.</summary>
    public static void Enter(UIElement element, double fromY = 10, int delay = 0, int ms = 500)
    {
        var (_, move) = Transforms(element);
        Animate(element, UIElement.OpacityProperty, 1, ms, delay: delay, from: 0);
        Animate(move, TranslateTransform.YProperty, 0, ms, delay: delay, from: fromY);
    }

    /// <summary>Fade in from slightly smaller: popups.</summary>
    public static void Pop(UIElement element, int ms = 260)
    {
        var (scale, _) = Transforms(element);
        Animate(element, UIElement.OpacityProperty, 1, ms, from: 0);
        Animate(scale, ScaleTransform.ScaleXProperty, 1, ms, from: 0.97);
        Animate(scale, ScaleTransform.ScaleYProperty, 1, ms, from: 0.97);
    }

    static (ScaleTransform Scale, TranslateTransform Move) Transforms(UIElement element)
    {
        if (element.RenderTransform is TransformGroup group && group.Children.Count == 2
            && group.Children[0] is ScaleTransform s && group.Children[1] is TranslateTransform t)
            return (s, t);
        var scale = new ScaleTransform();
        var move = new TranslateTransform();
        element.RenderTransform = new TransformGroup { Children = { scale, move } };
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return (scale, move);
    }

    // ---------- Enter on load ----------

    public static readonly DependencyProperty EnterOnLoadProperty = DependencyProperty.RegisterAttached(
        "EnterOnLoad", typeof(bool), typeof(Motion), new PropertyMetadata(false, (d, e) =>
        {
            if (d is FrameworkElement el && e.NewValue is true) el.Loaded += (_, _) => Enter(el);
        }));
    public static void SetEnterOnLoad(DependencyObject d, bool value) => d.SetValue(EnterOnLoadProperty, value);
    public static bool GetEnterOnLoad(DependencyObject d) => (bool)d.GetValue(EnterOnLoadProperty);

    /// <summary>Like EnterOnLoad, delayed by the item's position in its panel so grids cascade in.</summary>
    public static readonly DependencyProperty EnterStaggerProperty = DependencyProperty.RegisterAttached(
        "EnterStagger", typeof(bool), typeof(Motion), new PropertyMetadata(false, (d, e) =>
        {
            if (d is FrameworkElement el && e.NewValue is true)
                el.Loaded += (_, _) => Enter(el, fromY: 14, delay: Math.Min(IndexInPanel(el), 18) * 28, ms: 550);
        }));
    public static void SetEnterStagger(DependencyObject d, bool value) => d.SetValue(EnterStaggerProperty, value);
    public static bool GetEnterStagger(DependencyObject d) => (bool)d.GetValue(EnterStaggerProperty);

    static int IndexInPanel(DependencyObject element)
    {
        var child = element;
        var parent = VisualTreeHelper.GetParent(element);
        while (parent is not null and not Panel)
        {
            child = parent;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent is Panel panel ? panel.Children.IndexOf((UIElement)child) : 0;
    }

    // ---------- Hover lift ----------

    public static readonly DependencyProperty HoverLiftProperty = DependencyProperty.RegisterAttached(
        "HoverLift", typeof(bool), typeof(Motion), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not FrameworkElement el || e.NewValue is not true) return;
            var shadow = new DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Direction = 270, Color = Colors.Black, Opacity = 0 };
            el.Effect = shadow;
            el.MouseEnter += (_, _) =>
            {
                Animate(Transforms(el).Move, TranslateTransform.YProperty, -2, 300);
                Animate(shadow, DropShadowEffect.OpacityProperty, el.TryFindResource("ShadowOpacity") as double? ?? 0.12, 300);
            };
            el.MouseLeave += (_, _) =>
            {
                Animate(Transforms(el).Move, TranslateTransform.YProperty, 0, 300);
                Animate(shadow, DropShadowEffect.OpacityProperty, 0, 300);
            };
        }));
    public static void SetHoverLift(DependencyObject d, bool value) => d.SetValue(HoverLiftProperty, value);
    public static bool GetHoverLift(DependencyObject d) => (bool)d.GetValue(HoverLiftProperty);

    // ---------- Press scale ----------

    static readonly DependencyProperty PressHookedProperty = DependencyProperty.RegisterAttached("PressHooked", typeof(bool), typeof(Motion));

    /// <summary>Scale while pressed; 1 disables it. Handlers read the current value, so styles can override it.</summary>
    public static readonly DependencyProperty PressScaleProperty = DependencyProperty.RegisterAttached(
        "PressScale", typeof(double), typeof(Motion), new PropertyMetadata(1.0, (d, _) =>
        {
            if (d is not UIElement el || (bool)el.GetValue(PressHookedProperty)) return;
            el.SetValue(PressHookedProperty, true);
            el.PreviewMouseLeftButtonDown += (_, _) =>
            {
                var to = GetPressScale(el);
                if (to == 1) return;
                var (scale, _) = Transforms(el);
                Animate(scale, ScaleTransform.ScaleXProperty, to, 120);
                Animate(scale, ScaleTransform.ScaleYProperty, to, 120);
            };
            MouseButtonEventHandler release = (_, _) => Release(el);
            el.PreviewMouseLeftButtonUp += release;
            el.MouseLeave += (_, _) => Release(el);
        }));
    public static void SetPressScale(DependencyObject d, double value) => d.SetValue(PressScaleProperty, value);
    public static double GetPressScale(DependencyObject d) => (double)d.GetValue(PressScaleProperty);

    static void Release(UIElement el)
    {
        if (el.RenderTransform is not TransformGroup) return;
        var (scale, _) = Transforms(el);
        Animate(scale, ScaleTransform.ScaleXProperty, 1, 380, Spring);
        Animate(scale, ScaleTransform.ScaleYProperty, 1, 380, Spring);
    }

    // ---------- Ripple ----------

    public static readonly DependencyProperty RippleProperty = DependencyProperty.RegisterAttached(
        "Ripple", typeof(bool), typeof(Motion), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not FrameworkElement el || e.NewValue is not true) return;
            el.PreviewMouseLeftButtonDown += (_, args) =>
            {
                if (!Enabled || AdornerLayer.GetAdornerLayer(el) is not { } layer) return;
                var ripple = new RippleAdorner(el, args.GetPosition(el), (Brush)el.FindResource("Accent"));
                layer.Add(ripple);
                ripple.Run(() => layer.Remove(ripple));
            };
        }));
    public static void SetRipple(DependencyObject d, bool value) => d.SetValue(RippleProperty, value);
    public static bool GetRipple(DependencyObject d) => (bool)d.GetValue(RippleProperty);

    // ---------- Sliding selection pill ----------

    /// <summary>For a ListBox whose template has a "PART_Pill" inside a Canvas: the pill glides to the selected item.</summary>
    public static readonly DependencyProperty SlidingPillProperty = DependencyProperty.RegisterAttached(
        "SlidingPill", typeof(bool), typeof(Motion), new PropertyMetadata(false, (d, e) =>
        {
            if (d is not ListBox list || e.NewValue is not true) return;
            list.SelectionChanged += (_, _) => MovePill(list, 450);
            list.SizeChanged += (_, _) => MovePill(list, 0);
            list.Loaded += (_, _) => list.Dispatcher.BeginInvoke(() => MovePill(list, 0), DispatcherPriority.Loaded);
        }));
    public static void SetSlidingPill(DependencyObject d, bool value) => d.SetValue(SlidingPillProperty, value);
    public static bool GetSlidingPill(DependencyObject d) => (bool)d.GetValue(SlidingPillProperty);

    static void MovePill(ListBox list, int ms)
    {
        if (list.Template?.FindName("PART_Pill", list) is not FrameworkElement pill) return;
        if (list.SelectedItem is null || list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) is not FrameworkElement item
            || VisualTreeHelper.GetParent(pill) is not UIElement canvas || !item.IsLoaded)
        {
            pill.Visibility = Visibility.Collapsed;
            return;
        }
        var at = item.TranslatePoint(new Point(), canvas);
        if (pill.RenderTransform is not TranslateTransform move) pill.RenderTransform = move = new TranslateTransform();
        if (pill.Visibility != Visibility.Visible) ms = 0;
        pill.Visibility = Visibility.Visible;
        pill.Height = item.ActualHeight;
        Animate(pill, FrameworkElement.WidthProperty, item.ActualWidth, ms);
        Animate(move, TranslateTransform.XProperty, at.X, ms);
        Animate(move, TranslateTransform.YProperty, at.Y, ms);
    }
}

// Expanding accent circle from the click point, clipped to the element's rounded corners.
sealed class RippleAdorner : Adorner
{
    static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
        "Progress", typeof(double), typeof(RippleAdorner), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    readonly Point _center;
    readonly Brush _brush;
    readonly double _reach;

    public RippleAdorner(UIElement element, Point center, Brush brush) : base(element)
    {
        IsHitTestVisible = false;
        _center = center;
        _brush = brush;
        var size = element.RenderSize;
        _reach = Math.Sqrt(size.Width * size.Width + size.Height * size.Height);
    }

    public void Run(Action done)
    {
        var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(700)) { EasingFunction = Motion.EaseOut };
        animation.Completed += (_, _) => done();
        BeginAnimation(ProgressProperty, animation);
    }

    protected override void OnRender(DrawingContext dc)
    {
        var progress = (double)GetValue(ProgressProperty);
        var radius = 10 + progress * _reach;
        dc.PushClip(new RectangleGeometry(new Rect(AdornedElement.RenderSize), 12, 12));
        dc.PushOpacity(0.22 * (1 - progress));
        dc.DrawEllipse(_brush, null, _center, radius, radius);
        dc.Pop();
        dc.Pop();
    }
}
