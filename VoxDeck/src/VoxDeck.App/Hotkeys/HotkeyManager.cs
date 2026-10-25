using System.Windows.Input;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.Core.Hotkeys;

namespace VoxDeck.App.Hotkeys;

// Owns every global shortcut (one per sound plus Stop all) and the "press a key combination" capture.
// Any change re-registers everything; there are only ever a few dozen.
public sealed class HotkeyManager(LibraryViewModel library, SettingsViewModel settings, PlayerViewModel player, ToastsViewModel toasts)
{
    GlobalHotkeys? _hotkeys;
    Action<Hotkey?>? _onCaptured;
    Action? _onCancelled;

    public bool IsCapturing => _onCaptured is not null;

    public void Attach(IntPtr hwnd)
    {
        _hotkeys = new GlobalHotkeys(hwnd);
        var failed = RegisterAll();
        if (failed.Count > 0)
            toasts.Error($"{string.Join(", ", failed)} {(failed.Count == 1 ? "is" : "are")} already used by another app.");
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
                toasts.Info("Add Ctrl, Alt or Shift so normal typing still works. F-keys and the number pad work alone.");
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
        if (hotkey is not null && hotkey == settings.Current.StopAllHotkey)
        {
            toasts.Error($"{HotkeyText.Format(hotkey)} is your Stop all shortcut.");
            RegisterAll();
            return;
        }
        if (hotkey is not null)
        {
            foreach (var other in library.Items.Where(i => i != item && i.Model.Hotkey == hotkey).ToList())
            {
                library.Edit(other, other.Model with { Hotkey = null });
                toasts.Info($"Moved {HotkeyText.Format(hotkey)} from “{other.Model.Title}”.");
            }
        }
        library.Edit(item, item.Model with { Hotkey = hotkey });
        RegisterAll();
        if (hotkey is null) return;
        if (item.HotkeyFailed)
        {
            toasts.Error($"Windows says {HotkeyText.Format(hotkey)} is already used by another app. Try another one.");
            library.Edit(item, item.Model with { Hotkey = previous });
            RegisterAll();
        }
        else
        {
            toasts.Success($"{HotkeyText.Format(hotkey)} now plays “{item.Model.Title}”.");
        }
    }

    public void AssignStopAll(Hotkey? hotkey)
    {
        if (hotkey is not null && library.Items.FirstOrDefault(i => i.Model.Hotkey == hotkey) is { } owner)
        {
            toasts.Error($"{HotkeyText.Format(hotkey)} already plays “{owner.Model.Title}”.");
            RegisterAll();
            return;
        }
        var previous = settings.Current.StopAllHotkey;
        settings.Update(s => s with { StopAllHotkey = hotkey });
        if (RegisterAll().Count > 0 && hotkey is not null)
        {
            toasts.Error($"Windows says {HotkeyText.Format(hotkey)} is already used by another app. Try another one.");
            settings.Update(s => s with { StopAllHotkey = previous });
            RegisterAll();
        }
        else if (hotkey is not null)
        {
            toasts.Success($"{HotkeyText.Format(hotkey)} now stops all sounds.");
        }
    }

    List<string> RegisterAll()
    {
        var failed = new List<string>();
        if (_hotkeys is null || IsCapturing) return failed;
        _hotkeys.Clear();
        foreach (var item in library.Items)
        {
            item.HotkeyFailed = item.Model.Hotkey is { } h && !_hotkeys.Register(h, () => player.Play(item));
            if (item.HotkeyFailed) failed.Add(HotkeyText.Format(item.Model.Hotkey!));
        }
        if (settings.Current.StopAllHotkey is { } stop && !_hotkeys.Register(stop, player.StopEverything))
            failed.Add(HotkeyText.Format(stop));
        return failed;
    }
}
