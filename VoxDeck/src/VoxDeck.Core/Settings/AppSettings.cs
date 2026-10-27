using VoxDeck.Core.Audio;
using VoxDeck.Core.Hotkeys;

namespace VoxDeck.Core.Settings;

/// <param name="Theme">"System", "Light" or "Dark".</param>
/// <param name="MonitorDeviceId">Where you hear sounds; null follows the Windows default.</param>
/// <param name="CableDeviceId">Playback side of the virtual cable used for voice chat; null picks the first one found.</param>
/// <param name="SoundRoute">Who hears every sound: you, your voice chat, or both.</param>
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
    Hotkey? MuteMicHotkey = null,
    SoundRoute SoundRoute = SoundRoute.Both,
    bool OnboardingDone = false)
{
    public static readonly string DefaultPath = Path.Combine(JsonFile.AppFolder, "settings.json");

    // Older builds let the mixer faders reach 10× volume; pull saved values back into range.
    public static AppSettings Load(string path) => JsonFile.Load<AppSettings>(path) is { } s
        ? s with
        {
            MasterVolume = Math.Clamp(s.MasterVolume, 0, 1),
            CallSoundVolume = Math.Clamp(s.CallSoundVolume, 0, 1),
            MicGain = Math.Clamp(s.MicGain, 0, 2),
        }
        : new();

    public void Save(string path) => JsonFile.Save(path, this);
}
