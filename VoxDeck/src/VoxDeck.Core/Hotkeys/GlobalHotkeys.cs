using System.Runtime.InteropServices;

namespace VoxDeck.Core.Hotkeys;

// System-wide shortcuts via RegisterHotKey. Windows posts WM_HOTKEY to the window's thread.
// Hotkeys arrive on the UI thread; move them to a dedicated message thread if a busy UI ever delays them.
public sealed class GlobalHotkeys(IntPtr hwnd) : IDisposable
{
    const int WmHotkey = 0x0312;
    const uint NoRepeat = 0x4000;

    readonly Dictionary<int, Action> _actions = [];
    int _nextId = 1;

    /// <summary>Returns false when another app already owns this combination.</summary>
    public bool Register(Hotkey hotkey, Action action)
    {
        var id = _nextId++;
        if (!RegisterHotKey(hwnd, id, (uint)hotkey.Modifiers | NoRepeat, (uint)hotkey.VirtualKey)) return false;
        _actions[id] = action;
        return true;
    }

    public void Clear()
    {
        foreach (var id in _actions.Keys) UnregisterHotKey(hwnd, id);
        _actions.Clear();
    }

    /// <summary>Call from the window's message hook. Returns true when the message was one of our hotkeys.</summary>
    public bool HandleMessage(int message, IntPtr wParam)
    {
        if (message != WmHotkey || !_actions.TryGetValue(wParam.ToInt32(), out var action)) return false;
        action();
        return true;
    }

    public void Dispose() => Clear();

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
