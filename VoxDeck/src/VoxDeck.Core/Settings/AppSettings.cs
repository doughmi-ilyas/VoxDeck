using VoxDeck.Core.Hotkeys;

namespace VoxDeck.Core.Settings;

/// <param name="Theme">"System", "Light" or "Dark".</param>
public sealed record AppSettings(
    string Theme = "System",
    bool ReduceMotion = false,
    bool OverlapSounds = true,
    double MasterVolume = 1,
    Hotkey? StopAllHotkey = null,
    string? MicrophoneId = null)
{
    public static readonly string DefaultPath = Path.Combine(JsonFile.AppFolder, "settings.json");

    public static AppSettings Load(string path) => JsonFile.Load<AppSettings>(path) ?? new();

    public void Save(string path) => JsonFile.Save(path, this);
}
