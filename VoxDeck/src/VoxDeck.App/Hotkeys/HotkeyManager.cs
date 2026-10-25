using System.Windows.Input;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Mixer;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.Core.Hotkeys;
using VoxDeck.Core.Settings;

namespace VoxDeck.App.Hotkeys;

/// <summary>A shortcut stored in the settings rather than on a sound (Stop all, mute mic).</summary>
public sealed record AppShortcut(string Purpose, Func<AppSettings, Hotkey?> Get, Func<AppSettings, Hotkey?, AppSettings> With, Action Run);

// Owns every global shortcut (one per sound plus the app shortcuts) and the "press a key combination" capture.
// Any change re-registers everything; there are only ever a few dozen.
public sealed class HotkeyManager
{
    readonly LibraryViewModel _library;
    readonly SettingsViewModel _settings;
    readonly PlayerViewModel _player;
    readonly ToastsViewModel _toasts;
    GlobalHotkeys? _hotkeys;
    Action<Hotkey?>? _onCaptured;
    Action? _onCancelled;

    public HotkeyManager(LibraryViewModel library, SettingsViewModel settings, PlayerViewModel player, MixerViewModel mixer, ToastsViewModel toasts)
    {
        _library = library;
        _settings = settings;
        _player = player;
        _toasts = toasts;
        StopAll = new("stops all sounds", s => s.StopAllHotkey, (s, h) => s with { StopAllHotkey = h }, player.StopEverything);
        MuteMic = new("mutes your microphone", s => s.MuteMicHotkey, (s, h) => s with { MuteMicHotkey = h }, mixer.ToggleMuteFromHotkey);
        Shortcuts = [StopAll, MuteMic];
    }

    public AppShortcut StopAll { get; }
    public AppShortcut MuteMic { get; }
    public AppShortcut[] Shortcuts { get; }
    public bool IsCapturing => _onCaptured is not null;

    public void Attach(IntPtr hwnd)
    {
        _hotkeys = new GlobalHotkeys(hwnd);
        var failed = RegisterAll();
        if (failed.Count > 0)
            _toasts.Error($"{string.Join(", ", failed)} {(failed.Count == 1 ? "is" : "are")} already used by another app.");
    }

    public bool HandleMessage(int message, IntPtr wParam) => _hotkeys?.HandleMessage(message, wParam) == true;

    public void Refresh() => RegisterAll();

    /// <summary>Our own shortcuts are paused while capturing so pressing one doesn't trigger it.</summary>
    public void Capture(Action<Hotkey?> onCaptured, Action onCancelled)
    {
        _onCancelled?.Invoke();
        _onCaptured = onCaptured;
        _onCancelled = onCancelled;
        _hotkeys?.Clear();
    }

    public void CancelCapture()
    {
        var cancelled = _onCancelled;
        _onCaptured = null;
        _onCancelled = null;
        cancelled?.Invoke();
        RegisterAll();
    }

    /// <summary>Feed key presses here while capturing. Returns true when the key was consumed.</summary>
    public bool HandleKey(Key key, ModifierKeys modifiers)
    {
        if (_onCaptured is null) return false;
        if (key == Key.Escape)
        {
            CancelCapture();
            return true;
        }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin or Key.ImeProcessed or Key.DeadCharProcessed)
            return true; // wait for the real key

        Hotkey? result = null;
        if (!(key is Key.Back or Key.Delete && modifiers == ModifierKeys.None))
        {
            if (modifiers == ModifierKeys.None && key is not (>= Key.F1 and <= Key.F24) && key is not (>= Key.NumPad0 and <= Key.Divide))
            {
                _toasts.Info("Add Ctrl, Alt or Shift so normal typing still works. F-keys and the number pad work alone.");
                return true;
            }
            result = new Hotkey((KeyModifiers)modifiers, KeyInterop.VirtualKeyFromKey(key));
        }
        var done = _onCaptured;
        _onCaptured = null;
        _onCancelled = null;
        done(result);
        return true;
    }

    public void AssignToSound(SoundItem item, Hotkey? hotkey)
    {
        var previous = item.Model.Hotkey;
        if (hotkey is not null && Shortcuts.FirstOrDefault(s => s.Get(_settings.Current) == hotkey) is { } shortcut)
        {
            _toasts.Error($"{HotkeyText.Format(hotkey)} already {shortcut.Purpose}.");
            RegisterAll();
            return;
        }
        if (hotkey is not null)
        {
            foreach (var other in _library.Items.Where(i => i != item && i.Model.Hotkey == hotkey).ToList())
            {
                _library.Edit(other, other.Model with { Hotkey = null });
                _toasts.Info($"Moved {HotkeyText.Format(hotkey)} from “{other.Model.Title}”.");
            }
        }
        _library.Edit(item, item.Model with { Hotkey = hotkey });
        RegisterAll();
        if (hotkey is null) return;
        if (item.HotkeyFailed)
        {
            _toasts.Error($"Windows says {HotkeyText.Format(hotkey)} is already used by another app. Try another one.");
            _library.Edit(item, item.Model with { Hotkey = previous });
            RegisterAll();
        }
        else
        {
            _toasts.Success($"{HotkeyText.Format(hotkey)} now plays “{item.Model.Title}”.");
        }
    }

    public void Assign(AppShortcut shortcut, Hotkey? hotkey)
    {
        if (hotkey is not null)
        {
            if (_library.Items.FirstOrDefault(i => i.Model.Hotkey == hotkey) is { } owner)
            {
                _toasts.Error($"{HotkeyText.Format(hotkey)} already plays “{owner.Model.Title}”.");
                RegisterAll();
                return;
            }
            if (Shortcuts.FirstOrDefault(s => s != shortcut && s.Get(_settings.Current) == hotkey) is { } other)
            {
                _toasts.Error($"{HotkeyText.Format(hotkey)} already {other.Purpose}.");
                RegisterAll();
                return;
            }
        }
        var previous = shortcut.Get(_settings.Current);
        _settings.Update(s => shortcut.With(s, hotkey));
        var failed = RegisterAll();
        if (hotkey is not null && failed.Contains(HotkeyText.Format(hotkey)))
        {
            _toasts.Error($"Windows says {HotkeyText.Format(hotkey)} is already used by another app. Try another one.");
            _settings.Update(s => shortcut.With(s, previous));
            RegisterAll();
        }
        else if (hotkey is not null)
        {
            _toasts.Success($"{HotkeyText.Format(hotkey)} now {shortcut.Purpose}.");
        }
    }

    List<string> RegisterAll()
    {
        var failed = new List<string>();
        if (_hotkeys is null || IsCapturing) return failed;
        _hotkeys.Clear();
        foreach (var item in _library.Items)
        {
            item.HotkeyFailed = item.Model.Hotkey is { } h && !_hotkeys.Register(h, () => _player.Play(item));
            if (item.HotkeyFailed) failed.Add(HotkeyText.Format(item.Model.Hotkey!));
        }
        foreach (var shortcut in Shortcuts)
        {
            if (shortcut.Get(_settings.Current) is { } h && !_hotkeys.Register(h, shortcut.Run))
                failed.Add(HotkeyText.Format(h));
        }
        return failed;
    }
}
