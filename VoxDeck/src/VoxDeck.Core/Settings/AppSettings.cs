using VoxDeck.Core.Hotkeys;

namespace VoxDeck.Core.Settings;

/// <param name="Theme">"System", "Light" or "Dark".</param>
/// <param name="MonitorDeviceId">Where you hear sounds; null follows the Windows default.</param>
/// <param name="CableDeviceId">Playback side of the virtual cable used for voice chat; null picks the first one found.</param>
public sealed record AppSettings(
    string Theme = "System",
    bool ReduceMotion = false,
    bool OverlapSounds = true,
    double MasterVolume = 1,
    Hotkey? StopAllHotkey = null,
    string? MicrophoneId = null,
    string? MonitorDeviceId = null,
    bool VoiceMixEnabled = false,
    string? CableDeviceId = null,
    double MicGain = 1,
    bool MicMuted = false,
    double CallSoundVolume = 0.8,
    Hotkey? MuteMicHotkey = null)
{
    public static readonly string DefaultPath = Path.Combine(JsonFile.AppFolder, "settings.json");

    public static AppSettings Load(string path) => JsonFile.Load<AppSettings>(path) ?? new();

    public void Save(string path) => JsonFile.Save(path, this);
}
