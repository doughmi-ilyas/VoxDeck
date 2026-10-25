using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.Win32;
using VoxDeck.App.Controls;
using VoxDeck.App.Hotkeys;
using VoxDeck.App.Theme;
using VoxDeck.Core;
using VoxDeck.Core.Settings;

namespace VoxDeck.App.Features.Settings;

public sealed class SettingsViewModel : ViewModel
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
        CaptureStopAll = new Command(OnCaptureStopAll);
        ClearStopAll = new Command(() => Hotkeys?.AssignStopAll(null));
        OpenFolder = new Command(() => Process.Start("explorer.exe", JsonFile.AppFolder));
    }

    public string Title => "Settings";
    public string Icon => "";
    public string? Badge => null;

    public AppSettings Current { get; private set; }
    public HotkeyManager? Hotkeys { get; set; }

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

    public string? StopAllHotkeyText => Current.StopAllHotkey is { } h ? HotkeyText.Format(h) : null;
    public bool IsCapturingStopAll { get; private set => Set(ref field, value); }
    public string LibraryFolder => JsonFile.AppFolder;

    public Command CaptureStopAll { get; }
    public Command ClearStopAll { get; }
    public Command OpenFolder { get; }

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

    void OnCaptureStopAll()
    {
        if (Hotkeys is null) return;
        if (IsCapturingStopAll)
        {
            Hotkeys.CancelCapture();
            return;
        }
        IsCapturingStopAll = true;
        Hotkeys.Capture(
            hotkey =>
            {
                IsCapturingStopAll = false;
                Hotkeys.AssignStopAll(hotkey);
            },
            () => IsCapturingStopAll = false);
    }
}
