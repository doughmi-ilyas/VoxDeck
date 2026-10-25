using System.Runtime.InteropServices;
using System.Windows.Input;
using VoxDeck.Core.Hotkeys;

namespace VoxDeck.App.Hotkeys;

public static class HotkeyText
{
    /// <summary>"Ctrl+Shift+1", using the character the current keyboard layout prints on that key.</summary>
    public static string Format(Hotkey hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (hotkey.Modifiers.HasFlag(KeyModifiers.Windows)) parts.Add("Win");
        parts.Add(KeyName(hotkey.VirtualKey));
        return string.Join("+", parts);
    }

    static string KeyName(int virtualKey)
    {
        var key = KeyInterop.KeyFromVirtualKey(virtualKey);
        if (key is >= Key.NumPad0 and <= Key.NumPad9) return $"Num {key - Key.NumPad0}";
        if (key is >= Key.F1 and <= Key.F24) return key.ToString();
        var ch = MapVirtualKey((uint)virtualKey, 2) & 0x7FFF; // MAPVK_VK_TO_CHAR; the high bit marks dead keys
        if (ch > ' ') return char.ToUpper((char)ch).ToString();
        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "Enter",
            Key.PageUp => "PgUp",
            Key.PageDown => "PgDn",
            Key.Insert => "Ins",
            _ => key.ToString(),
        };
    }

    [DllImport("user32.dll")]
    static extern uint MapVirtualKey(uint code, uint mapType);
}
