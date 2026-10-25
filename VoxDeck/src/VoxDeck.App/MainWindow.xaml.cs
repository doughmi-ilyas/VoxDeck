using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VoxDeck.App.Controls;
using VoxDeck.App.Theme;

namespace VoxDeck.App;

public partial class MainWindow : Window
{
    readonly MainViewModel _vm;
    IntPtr _hwnd;
    bool _drawerOpen;

    public MainWindow()
    {
        InitializeComponent();
        // Fit small and low-resolution screens.
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.Editor.PropertyChanged += OnEditorChanged;
        ThemeManager.Changed += ApplyWindowTheme;
        Loaded += (_, _) => TitleMark.Play();
        Closed += (_, _) => _vm.Dispose();
        StateChanged += (_, _) =>
        {
            // A maximized chrome-less window extends past the screen edge by the resize border.
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
            MaxButton.Content = WindowState == WindowState.Maximized ? "" : "";
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var source = (HwndSource)PresentationSource.FromVisual(this);
        _hwnd = source.Handle;
        source.AddHook(WndProc);
        var round = 2; // DWMWCP_ROUND
        DwmSetWindowAttribute(_hwnd, 33, ref round, sizeof(int));
        ApplyWindowTheme();
        _vm.Hotkeys.Attach(_hwnd);
    }

    IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_vm.Hotkeys.HandleMessage(message, wParam)) handled = true;
        return IntPtr.Zero;
    }

    // Keeps the resize border and window shadow in the right theme.
    void ApplyWindowTheme()
    {
        if (_hwnd == IntPtr.Zero) return;
        var dark = ThemeManager.IsDark ? 1 : 0;
        DwmSetWindowAttribute(_hwnd, 20, ref dark, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (_vm.Hotkeys.HandleKey(key, Keyboard.Modifiers))
        {
            e.Handled = true;
            return;
        }
        if (key != Key.Escape) return;
        if (_vm.Palette.IsOpen) _vm.Palette.Close();
        else if (_vm.Editor.IsOpen) _vm.Editor.Close();
        else return;
        e.Handled = true;
    }

    void OnEditorChanged(object? sender, PropertyChangedEventArgs e)
    {
        var open = _vm.Editor.IsOpen;
        if (open == _drawerOpen) return;
        _drawerOpen = open;
        if (open)
        {
            Drawer.Visibility = Visibility.Visible;
            Motion.Animate(DrawerShift, TranslateTransform.XProperty, 0, 500);
            EditorView.AnimateIn();
        }
        else
        {
            Motion.Animate(DrawerShift, TranslateTransform.XProperty, Drawer.Width + 40, 280, Motion.Ease,
                done: () => { if (!_drawerOpen) Drawer.Visibility = Visibility.Collapsed; });
        }
    }

    void OnDragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop)) _vm.IsDraggingFiles = true;
    }

    void OnDragLeave(object sender, DragEventArgs e) => _vm.IsDraggingFiles = false;

    void OnDrop(object sender, DragEventArgs e)
    {
        _vm.IsDraggingFiles = false;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) _vm.Library.Import(files);
    }

    void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    void OnClose(object sender, RoutedEventArgs e) => Close();

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
