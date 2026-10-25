using VoxDeck.App.Hotkeys;

namespace VoxDeck.App.Features.Settings;

// One "click, then press a key combination" row for an app shortcut.
public sealed class ShortcutSetting : ViewModel
{
    readonly HotkeyManager _hotkeys;
    readonly AppShortcut _shortcut;
    readonly SettingsViewModel _settings;

    public ShortcutSetting(HotkeyManager hotkeys, AppShortcut shortcut, SettingsViewModel settings)
    {
        _hotkeys = hotkeys;
        _shortcut = shortcut;
        _settings = settings;
        settings.PropertyChanged += (_, _) => Raise(nameof(Text));
        Capture = new Command(OnCapture);
        Clear = new Command(() => hotkeys.Assign(shortcut, null));
    }

    public string? Text => _shortcut.Get(_settings.Current) is { } h ? HotkeyText.Format(h) : null;
    public bool IsCapturing { get; private set => Set(ref field, value); }

    public Command Capture { get; }
    public Command Clear { get; }

    void OnCapture()
    {
        if (IsCapturing)
        {
            _hotkeys.CancelCapture();
            return;
        }
        IsCapturing = true;
        _hotkeys.Capture(
            hotkey =>
            {
                IsCapturing = false;
                _hotkeys.Assign(_shortcut, hotkey);
            },
            () => IsCapturing = false);
    }
}
