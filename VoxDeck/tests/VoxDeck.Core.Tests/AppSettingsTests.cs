using VoxDeck.Core.Hotkeys;
using VoxDeck.Core.Settings;

namespace VoxDeck.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public void SaveThenLoad_KeepsHotkeyAndTheme()
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxdeck-settings-{Guid.NewGuid()}.json");
        var settings = new AppSettings(Theme: "Dark", StopAllHotkey: new Hotkey(KeyModifiers.Control | KeyModifiers.Shift, 0x53));
        settings.Save(path);
        Assert.Equal(settings, AppSettings.Load(path));
        File.Delete(path);
    }

    [Fact]
    public void Load_PullsOutOfRangeVolumesBackIn()
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxdeck-settings-{Guid.NewGuid()}.json");
        new AppSettings(MasterVolume: 10, CallSoundVolume: 7.6, MicGain: 5).Save(path);
        var loaded = AppSettings.Load(path);
        File.Delete(path);
        Assert.Equal((1d, 1d, 2d), (loaded.MasterVolume, loaded.CallSoundVolume, loaded.MicGain));
    }

    [Fact]
    public void MissingFile_GivesDefaults() =>
        Assert.Equal(new AppSettings(), AppSettings.Load(Path.Combine(Path.GetTempPath(), "voxdeck-none.json")));
}
