using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using VoxDeck.App.Controls;
using VoxDeck.App.Features.AudioSetup;
using VoxDeck.App.Features.Settings;

namespace VoxDeck.App.Features.Onboarding;

// All the motion lives here: splash entrance, ambient drift, step transitions, the live mic orb, pings, sparks and confetti.
// Ambient loops only run while the onboarding is on screen, and none of it runs with Reduce motion on.
public partial class OnboardingView : UserControl
{
    static readonly Random Rng = new();
    static readonly IEasingFunction QuadIn = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseIn });
    static readonly IEasingFunction QuadOut = Frozen(new QuadraticEase { EasingMode = EasingMode.EaseOut });

    readonly List<(IAnimatable Target, DependencyProperty Property)> _ambient = [];
    readonly List<TranslateTransform> _letters = [];
    OnboardingViewModel? _vm;
    int _shownStep = -1;
    bool _wasCapturing;

    public OnboardingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    StackPanel[] Bodies => [Body1, Body2, Body3, Body4, Body5];
    FrameworkElement[] Visuals => [Vis1, Vis2, Vis3, Vis4, Vis5];

    static IEasingFunction Frozen(EasingFunctionBase ease)
    {
        ease.Freeze();
        return ease;
    }

    void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is OnboardingViewModel old)
        {
            old.PropertyChanged -= OnVmChanged;
            old.Finished -= Leave;
            old.Opened -= PlaySplash;
            old.Audio.PropertyChanged -= OnAudioChanged;
            if (old.StopAll is { } s) s.PropertyChanged -= OnShortcutChanged;
        }
        _vm = e.NewValue as OnboardingViewModel;
        if (_vm is null) return;
        _vm.PropertyChanged += OnVmChanged;
        _vm.Finished += Leave;
        _vm.Opened += PlaySplash;
        _vm.Audio.PropertyChanged += OnAudioChanged;
        if (_vm.StopAll is { } shortcut) shortcut.PropertyChanged += OnShortcutChanged;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildWordmark();
        BuildWaves();
        BuildSpectrum();
        if (_vm?.IsOpen == true) PlaySplash();
    }

    // ---------------- Splash ----------------

    void PlaySplash()
    {
        _shownStep = OnboardingViewModel.Splash;
        Root.BeginAnimation(OpacityProperty, null);
        Root.Opacity = 1;
        Root.Effect = null;
        RootScale.ScaleX = RootScale.ScaleY = 1;
        DoneScreen.Visibility = FlowScreen.Visibility = Visibility.Collapsed;
        DoneScreen.Opacity = FlowScreen.Opacity = 0;
        ShowScreen(SplashScreen);

        SplashMark.Play();
        for (var i = 0; i < _letters.Count; i++)
        {
            Motion.Animate(_letters[i], TranslateTransform.YProperty, 0, 800, delay: 450 + i * 45, from: 90);
            Motion.Animate((UIElement)((Border)Wordmark.Children[i]).Child, OpacityProperty, 1, 800, delay: 450 + i * 45, from: 0);
        }
        Motion.Enter(SplashSub, fromY: 12, delay: 900, ms: 800);
        Motion.Enter(SplashCta, fromY: 12, delay: 1100, ms: 800);
        Motion.Animate(Spectrum, OpacityProperty, 1, 1600, delay: 900, from: 0);
        StartAmbient();
    }

    void BuildWordmark()
    {
        if (Wordmark.Children.Count > 0) return;
        foreach (var letter in "VoxDeck")
        {
            var text = new TextBlock
            {
                Text = letter.ToString(),
                FontSize = 64,
                FontWeight = FontWeights.Bold,
                FontFamily = (FontFamily)FindResource("DisplayFont"),
                Opacity = 0,
            };
            var move = new TranslateTransform();
            text.RenderTransform = move;
            _letters.Add(move);
            // Each letter rises out of its own mask; the slight overlap tightens the tracking like the design.
            Wordmark.Children.Add(new Border { ClipToBounds = true, Child = text, Padding = new Thickness(0, 6, 0, 10), Margin = new Thickness(0, -6, -2, -10) });
        }
    }

    void BuildWaves()
    {
        if (Waves.Children.Count > 0) return;
        for (var i = 0; i < 3; i++)
        {
            var ring = new Ellipse
            {
                Width = 400,
                Height = 400,
                StrokeThickness = 1,
                Opacity = 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(0.35, 0.35),
            };
            ring.SetResourceReference(Shape.StrokeProperty, "Line2");
            Canvas.SetLeft(ring, -200);
            Canvas.SetTop(ring, -200);
            Waves.Children.Add(ring);
        }
    }

    void BuildSpectrum()
    {
        if (Spectrum.Children.Count > 0) return;
        for (var i = 0; i < 44; i++)
            Spectrum.Children.Add(new Rectangle
            {
                Margin = new Thickness(2, 0, 2, 0),
                RadiusX = 3,
                RadiusY = 3,
                Opacity = 0.5,
                RenderTransformOrigin = new Point(0.5, 1),
                RenderTransform = new ScaleTransform(1, 0.08),
            });
    }

    // ---------------- Steps ----------------

    void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_vm is null) return;
        if (e.PropertyName == nameof(OnboardingViewModel.Step)) ShowStep(_vm.Step);
        else if (e.PropertyName == nameof(OnboardingViewModel.WantsVoiceChat))
            Motion.Animate(VoicePart, OpacityProperty, _vm.WantsVoiceChat ? 1 : 0.25, 400);
    }

    void ShowStep(int step)
    {
        var from = _shownStep;
        _shownStep = step;
        var dir = step >= from ? 1 : -1;
        Motion.Animate(AuroraShift, TranslateTransform.XProperty, -step * 40, 1400, Motion.Ease);

        if (step == OnboardingViewModel.Splash)
        {
            HideScreen(FlowScreen);
            PlaySplash();
            return;
        }
        if (step == OnboardingViewModel.Done)
        {
            HideScreen(FlowScreen);
            ShowScreen(DoneScreen);
            var badge = (ScaleTransform)DoneBadge.RenderTransform;
            Motion.Animate(badge, ScaleTransform.ScaleXProperty, 1, 600, Motion.Spring, 150, from: 0.4);
            Motion.Animate(badge, ScaleTransform.ScaleYProperty, 1, 600, Motion.Spring, 150, from: 0.4);
            Dispatcher.InvokeAsync(() => Burst(new Point(ActualWidth / 2, ActualHeight * 0.36), 90, 420, confetti: true),
                System.Windows.Threading.DispatcherPriority.Loaded);
            return;
        }
        if (from is OnboardingViewModel.Splash or OnboardingViewModel.Done or < 0)
        {
            HideScreen(SplashScreen);
            HideScreen(DoneScreen);
            ShowScreen(FlowScreen);
            StopSplashLoops();
        }

        for (var i = 0; i < Bodies.Length; i++) Bodies[i].Visibility = i == step - 1 ? Visibility.Visible : Visibility.Collapsed;
        BuildTitle(OnboardingViewModel.Steps[step - 1].Title);

        // The question slides in from the direction you're going, one piece after another.
        var index = 0;
        foreach (UIElement child in Left.Children)
        {
            if (child == TitleWords) continue;
            var delay = 60 + index++ * 70;
            Motion.Animate(child, OpacityProperty, 1, 700, delay: delay, from: 0);
            Motion.Animate(Move(child), TranslateTransform.XProperty, 0, 700, delay: delay, from: 28 * dir);
        }

        for (var i = 0; i < Visuals.Length; i++)
        {
            var visual = Visuals[i];
            var scale = Scale(visual);
            if (i == step - 1)
            {
                Motion.Animate(visual, OpacityProperty, 1, 550, delay: 100);
                Motion.Animate(scale, ScaleTransform.ScaleXProperty, 1, 800, delay: 100, from: 0.94);
                Motion.Animate(scale, ScaleTransform.ScaleYProperty, 1, 800, delay: 100, from: 0.94);
            }
            else
            {
                Motion.Animate(visual, OpacityProperty, 0, 300);
            }
        }
        // The voice-chat half of the diagram dims when you choose "only me".
        Motion.Animate(VoicePart, OpacityProperty, _vm!.WantsVoiceChat ? 1 : 0.25, 400);
    }

    // Each word of the headline rises out of a mask, staggered.
    void BuildTitle(string title)
    {
        TitleWords.Children.Clear();
        var words = title.Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            var text = new TextBlock { Text = words[i], FontSize = 34, FontWeight = FontWeights.Bold, FontFamily = (FontFamily)FindResource("DisplayFont") };
            var move = new TranslateTransform();
            text.RenderTransform = move;
            TitleWords.Children.Add(new Border { ClipToBounds = true, Child = text, Margin = new Thickness(0, 0, 9, 0), Padding = new Thickness(0, 0, 0, 4) });
            Motion.Animate(move, TranslateTransform.YProperty, 0, 800, delay: 60 + i * 45, from: 46);
            Motion.Animate(text, OpacityProperty, 1, 800, delay: 60 + i * 45, from: 0);
        }
    }

    // ---------------- Live visuals ----------------

    // The orb's rings breathe with your voice.
    void OnAudioChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AudioSetupViewModel.Level) || _vm is null) return;
        var lv = Motion.Enabled ? _vm.Audio.Level : 0;
        SetRing(Ring1, 0.5 + lv * 0.2, 0.15 + lv * 0.6);
        SetRing(Ring2, 0.62 + lv * 0.3, 0.08 + lv * 0.4);
        SetRing(Ring3, 0.74 + lv * 0.36, 0.04 + lv * 0.25);
        var orb = (ScaleTransform)Orb.RenderTransform;
        orb.ScaleX = orb.ScaleY = 1 + lv * 0.1;
    }

    static void SetRing(Ellipse ring, double scale, double opacity)
    {
        var t = (ScaleTransform)ring.RenderTransform;
        t.ScaleX = t.ScaleY = scale;
        ring.Opacity = opacity;
    }

    // Test sound: the speaker thumps, rings ripple out, the side arcs light up.
    void OnTestSound(object sender, RoutedEventArgs e)
    {
        var core = (ScaleTransform)SpeakerCore.RenderTransform;
        Keyframes(core, ScaleTransform.ScaleXProperty, 500, (0.4, 1.08), (1, 1));
        Keyframes(core, ScaleTransform.ScaleYProperty, 500, (0.4, 1.08), (1, 1));
        foreach (var (ping, delay) in new[] { (Ping1, 0), (Ping2, 180) })
        {
            var s = (ScaleTransform)ping.RenderTransform;
            Motion.Animate(s, ScaleTransform.ScaleXProperty, 1.9, 1100, delay: delay, from: 1);
            Motion.Animate(s, ScaleTransform.ScaleYProperty, 1.9, 1100, delay: delay, from: 1);
            Motion.Animate(ping, OpacityProperty, 0, 1100, delay: delay, from: 0.9);
        }
        foreach (var arc in new[] { ArcLeft, ArcRight })
            Motion.Animate(arc, OpacityProperty, 0.15, 1200, delay: 100, from: 1);
    }

    // A shortcut was set: the keycap pops and throws sparks.
    void OnShortcutChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShortcutSetting.IsCapturing) || sender is not ShortcutSetting shortcut) return;
        if (_wasCapturing && !shortcut.IsCapturing && shortcut.Text is not null && _shownStep == 4)
        {
            var cap = (ScaleTransform)Keycap.RenderTransform;
            Keyframes(cap, ScaleTransform.ScaleXProperty, 450, (0.4, 1.1), (1, 1));
            Keyframes(cap, ScaleTransform.ScaleYProperty, 450, (0.4, 1.1), (1, 1));
            var center = Keycap.TranslatePoint(new Point(Keycap.ActualWidth / 2, Keycap.ActualHeight / 2), Particles);
            Burst(center, 26, 150, confetti: false);
        }
        _wasCapturing = shortcut.IsCapturing;
    }

    // ---------------- Particles ----------------

    void Burst(Point origin, int count, double reach, bool confetti)
    {
        if (!Motion.Enabled) return;
        Color[] colors = [Accent(), Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x06, 0xB6, 0xD4), Color.FromRgb(0xE2, 0xA6, 0x34), Color.FromRgb(0x1F, 0x9D, 0x57)];
        for (var i = 0; i < count; i++)
        {
            var piece = new Rectangle
            {
                Width = confetti ? 8 : 6,
                Height = confetti ? 12 : 6,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(colors[Rng.Next(colors.Length)]),
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            var spin = new RotateTransform(Rng.Next(360));
            var move = new TranslateTransform();
            piece.RenderTransform = new TransformGroup { Children = { spin, move } };
            Canvas.SetLeft(piece, origin.X - piece.Width / 2);
            Canvas.SetTop(piece, origin.Y - piece.Height / 2);
            Particles.Children.Add(piece);

            var angle = Rng.NextDouble() * Math.PI * 2;
            var distance = reach * (0.45 + Rng.NextDouble() * 0.75);
            var dx = Math.Cos(angle) * distance;
            var dy = confetti ? -Math.Abs(Math.Sin(angle) * distance) - 80 : Math.Sin(angle) * distance;
            var ms = confetti ? 1700 + Rng.Next(700) : 700 + Rng.Next(300);
            Motion.Animate(move, TranslateTransform.XProperty, dx, ms, QuadOut);
            // Confetti flies up, then falls; sparks just fly out.
            var y = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms) };
            y.KeyFrames.Add(new EasingDoubleKeyFrame(dy, KeyTime.FromPercent(0.4), QuadOut));
            y.KeyFrames.Add(new EasingDoubleKeyFrame(dy + (confetti ? 520 : 20), KeyTime.FromPercent(1), QuadIn));
            move.BeginAnimation(TranslateTransform.YProperty, y);
            Motion.Animate(spin, RotateTransform.AngleProperty, spin.Angle + Rng.Next(-540, 540), ms, QuadOut);
            Motion.Animate(piece, OpacityProperty, 0, ms / 2, delay: ms / 2, from: 1, done: () => Particles.Children.Remove(piece));
        }
    }

    Color Accent() => (TryFindResource("Accent") as SolidColorBrush)?.Color ?? Color.FromRgb(0x5B, 0x82, 0xF5);

    // ---------------- Ambient loops ----------------

    void StartAmbient()
    {
        StopAmbient();
        if (!Motion.Enabled) return;
        // Aurora blobs drift slowly back and forth.
        var blobs = Aurora.Children.OfType<Ellipse>().ToArray();
        double[] seconds = [24, 30, 36];
        for (var i = 0; i < blobs.Length; i++)
        {
            var t = (TranslateTransform)blobs[i].RenderTransform;
            Loop(t, TranslateTransform.XProperty, i % 2 == 0 ? 110 : -90, seconds[i], -seconds[i] * 0.3 * i);
            Loop(t, TranslateTransform.YProperty, i % 2 == 0 ? -70 : 80, seconds[i] * 0.8, -seconds[i] * 0.2 * i);
        }
        // Rings ripple out from the logo, two seconds apart.
        for (var i = 0; i < Waves.Children.Count; i++)
        {
            var ring = (Ellipse)Waves.Children[i];
            var s = (ScaleTransform)ring.RenderTransform;
            var grow = new DoubleAnimation(0.35, 2.4, TimeSpan.FromSeconds(6)) { RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(i * 2), EasingFunction = Motion.Ease };
            s.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            s.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
            var fade = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromSeconds(6), RepeatBehavior = RepeatBehavior.Forever, BeginTime = TimeSpan.FromSeconds(i * 2) };
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(0)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0.9, KeyTime.FromPercent(0.15)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromPercent(1)));
            ring.BeginAnimation(OpacityProperty, fade);
            _ambient.Add((s, ScaleTransform.ScaleXProperty));
            _ambient.Add((s, ScaleTransform.ScaleYProperty));
            _ambient.Add((ring, OpacityProperty));
        }
        // Spectrum bars bob at their own pace.
        var accent = Accent();
        foreach (Rectangle bar in Spectrum.Children)
        {
            bar.Fill = new LinearGradientBrush(accent, Color.FromArgb(0, accent.R, accent.G, accent.B), 90);
            var s = (ScaleTransform)bar.RenderTransform;
            var bob = new DoubleAnimation(0.08, 0.2 + Rng.NextDouble() * 0.8, TimeSpan.FromMilliseconds(600 + Rng.Next(900)))
            {
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(Rng.Next(800)),
                EasingFunction = Motion.Ease,
            };
            s.BeginAnimation(ScaleTransform.ScaleYProperty, bob);
            _ambient.Add((s, ScaleTransform.ScaleYProperty));
        }
        // The orb's arc spins; the diagram's lines flow.
        var arc = (RotateTransform)OrbArc.RenderTransform;
        arc.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(2.8)) { RepeatBehavior = RepeatBehavior.Forever });
        _ambient.Add((arc, RotateTransform.AngleProperty));
        foreach (var line in new[] { FlowIn, FlowOut })
        {
            line.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(0, -8, TimeSpan.FromSeconds(1.2)) { RepeatBehavior = RepeatBehavior.Forever });
            _ambient.Add((line, Shape.StrokeDashOffsetProperty));
        }
    }

    void Loop(IAnimatable target, DependencyProperty property, double to, double seconds, double startSeconds)
    {
        var animation = new DoubleAnimation(0, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = Motion.Ease,
            BeginTime = TimeSpan.FromSeconds(startSeconds), // negative: start part-way through, so the blobs aren't in sync
        };
        target.BeginAnimation(property, animation);
        _ambient.Add((target, property));
    }

    // The splash's rings and spectrum aren't visible after the splash; stop them to save CPU.
    void StopSplashLoops()
    {
        foreach (var (target, property) in _ambient.Where(a => a.Target is ScaleTransform or Ellipse { Parent: Canvas }).ToList())
        {
            target.BeginAnimation(property, null);
            _ambient.Remove((target, property));
        }
    }

    void StopAmbient()
    {
        foreach (var (target, property) in _ambient) target.BeginAnimation(property, null);
        _ambient.Clear();
    }

    // ---------------- Screens ----------------

    static void ShowScreen(FrameworkElement screen)
    {
        screen.Visibility = Visibility.Visible;
        Motion.Animate(screen, OpacityProperty, 1, 600, from: screen.Opacity);
        if (screen.RenderTransform is ScaleTransform s)
        {
            Motion.Animate(s, ScaleTransform.ScaleXProperty, 1, 800, from: 0.98);
            Motion.Animate(s, ScaleTransform.ScaleYProperty, 1, 800, from: 0.98);
        }
    }

    static void HideScreen(FrameworkElement screen)
    {
        if (screen.Visibility != Visibility.Visible) return;
        Motion.Animate(screen, OpacityProperty, 0, 350, done: () =>
        {
            if (screen.Opacity < 0.01) screen.Visibility = Visibility.Collapsed;
        });
    }

    // Finish: the whole thing blurs, grows a touch and fades; then the app slides in (MainWindow).
    void Leave()
    {
        var blur = new BlurEffect { Radius = 0 };
        Root.Effect = Motion.Enabled ? blur : null;
        Motion.Animate(blur, BlurEffect.RadiusProperty, 10, 700);
        Motion.Animate(RootScale, ScaleTransform.ScaleXProperty, 1.04, 700);
        Motion.Animate(RootScale, ScaleTransform.ScaleYProperty, 1.04, 700);
        Motion.Animate(Root, OpacityProperty, 0, 700, done: () =>
        {
            StopAmbient();
            Root.Effect = null;
            Particles.Children.Clear();
            _vm?.Close();
        });
    }

    // ---------------- Helpers ----------------

    static TranslateTransform Move(UIElement element)
    {
        if (element.RenderTransform is TranslateTransform t) return t;
        t = new TranslateTransform();
        element.RenderTransform = t;
        return t;
    }

    static ScaleTransform Scale(FrameworkElement element)
    {
        if (element.RenderTransform is ScaleTransform s) return s;
        s = new ScaleTransform();
        element.RenderTransform = s;
        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return s;
    }

    static void Keyframes(IAnimatable target, DependencyProperty property, int ms, params (double At, double Value)[] frames)
    {
        if (!Motion.Enabled) return;
        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(ms) };
        foreach (var (at, value) in frames) animation.KeyFrames.Add(new EasingDoubleKeyFrame(value, KeyTime.FromPercent(at), Motion.EaseOut));
        target.BeginAnimation(property, animation);
    }
}
