using VoxDeck.Core.Library;

namespace VoxDeck.Core.Tests;

public class SoundLibraryTests
{
    readonly string _path = Path.Combine(Path.GetTempPath(), $"voxdeck-{Guid.NewGuid()}.json");

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        var sounds = new[] { new Sound("Air horn", @"C:\Sons d'été\air horn.mp3") };
        SoundLibrary.Save(_path, sounds);
        Assert.Equal(sounds, SoundLibrary.Load(_path));
        File.Delete(_path);
    }

    [Fact]
    public void OldLibraryFile_GetsDefaultSettings()
    {
        File.WriteAllText(_path, """[{"Title":"Boo","Path":"C:\\boo.wav"}]""");
        Assert.Equal(new Sound("Boo", @"C:\boo.wav", StartSeconds: 0, Volume: 1, Loop: false), SoundLibrary.Load(_path).Single());
        File.Delete(_path);
    }

    [Fact]
    public void CorruptFile_IsKeptAsBackup()
    {
        File.WriteAllText(_path, "{ not json");
        Assert.Empty(SoundLibrary.Load(_path));
        Assert.Equal("{ not json", File.ReadAllText(_path + ".corrupt"));
        File.Delete(_path);
        File.Delete(_path + ".corrupt");
    }
}
