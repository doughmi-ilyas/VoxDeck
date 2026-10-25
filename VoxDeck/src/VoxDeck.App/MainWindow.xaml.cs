using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace VoxDeck.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Fit small and low-resolution screens.
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        var vm = new MainViewModel();
        DataContext = vm;
        Closed += (_, _) => vm.Dispose();
    }

    // Dark title bar matching the sidebar. Older Windows builds ignore these attributes.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        var dark = 1;
        DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int));       // DWMWA_USE_IMMERSIVE_DARK_MODE
        var caption = 0x00131110;                                       // #101113 as COLORREF (0x00BBGGRR)
        DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));    // DWMWA_CAPTION_COLOR, Windows 11
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
