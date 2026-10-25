using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using VoxDeck.App.Controls;
using VoxDeck.App.Hotkeys;
using VoxDeck.App.Theme;
using VoxDeck.Core;
using VoxDeck.Core.Settings;

namespace VoxDeck.App.Features.Settings;

public sealed class SettingsViewModel : ViewModel, IPage
{
    readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    public SettingsViewModel()
    {
        Current = AppSettings.Load(AppSettings.DefaultPath);
        _saveTimer.Tick += (_, _) => SaveNow();
        Motion.UserReduced = Current.ReduceMotion;
        ThemeManager.Apply(Current.Theme);
        var dispatcher = Dispatcher.CurrentDispatcher;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && Current.Theme == "System")
                dispatcher.InvokeAsync(() => ThemeManager.Apply("System"));
        };
        OpenFolder = new Command(() => Process.Start("explorer.exe", JsonFile.AppFolder));
    }

    public string Title => "Settings";
    public string Icon => "";
    public string? Badge => null;

    public AppSettings Current { get; private set; }

    public ShortcutSetting? StopAllShortcut { get; private set; }
    public ShortcutSetting? MuteMicShortcut { get; private set; }

    public string[] Themes { get; } = ["System", "Light", "Dark"];

    public string Theme
    {
        get => Current.Theme;
        set
        {
            Update(s => s with { Theme = value });
            ThemeManager.Apply(value);
        }
    }

    public bool ReduceMotion
    {
        get => Current.ReduceMotion;
        set
        {
            Update(s => s with { ReduceMotion = value });
            Motion.UserReduced = value;
        }
    }

    public bool OverlapSounds
    {
        get => Current.OverlapSounds;
        set => Update(s => s with { OverlapSounds = value });
    }

    public string LibraryFolder => JsonFile.AppFolder;

    public Command OpenFolder { get; }

    public void AttachHotkeys(HotkeyManager hotkeys)
    {
        StopAllShortcut = new ShortcutSetting(hotkeys, hotkeys.StopAll, this);
        MuteMicShortcut = new ShortcutSetting(hotkeys, hotkeys.MuteMic, this);
        Raise("");
    }

    public void Update(Func<AppSettings, AppSettings> change)
    {
        Current = change(Current);
        _saveTimer.Stop();
        _saveTimer.Start();
        Raise("");
    }

    public void SaveNow()
    {
        _saveTimer.Stop();
        Current.Save(AppSettings.DefaultPath);
    }
}
