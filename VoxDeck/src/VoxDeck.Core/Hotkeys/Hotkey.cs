namespace VoxDeck.Core.Hotkeys;

/// <summary>Same values as the MOD_* flags RegisterHotKey expects (and WPF's ModifierKeys).</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Windows = 8,
}

public sealed record Hotkey(KeyModifiers Modifiers, int VirtualKey);
