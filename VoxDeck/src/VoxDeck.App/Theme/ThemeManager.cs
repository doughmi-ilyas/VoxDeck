using System.Windows;
using Microsoft.Win32;

namespace VoxDeck.App.Theme;

// Swaps the color dictionary (first merged dictionary in App.xaml). Styles use DynamicResource, so everything repaints.
public static class ThemeManager
{
    public static bool IsDark { get; private set; }

    public static event Action? Changed;

    /// <param name="theme">"System", "Light" or "Dark".</param>
    public static void Apply(string theme)
    {
        IsDark = theme switch
        {
            "Dark" => true,
            "Light" => false,
            _ => Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")
                ?.GetValue("AppsUseLightTheme") is 0,
        };
        Application.Current.Resources.MergedDictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Theme/{(IsDark ? "Dark" : "Light")}.xaml"),
        };
        Changed?.Invoke();
    }
}
