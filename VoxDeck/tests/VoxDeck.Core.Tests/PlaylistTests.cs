using VoxDeck.Core.Library;

namespace VoxDeck.Core.Tests;

public class PlaylistTests
{
    static readonly Playlist Abc = new("Chill", [@"C:\a.mp3", @"C:\b.mp3", @"C:\c.mp3"]);

    [Fact]
    public void Add_AppendsOnce_IgnoringCase()
    {
        var added = Abc.Add(@"C:\d.mp3").Add(@"c:\D.MP3");
        Assert.Equal([@"C:\a.mp3", @"C:\b.mp3", @"C:\c.mp3", @"C:\d.mp3"], added.Paths);
    }

    [Fact]
    public void Remove_KeepsOrderOfTheRest() =>
        Assert.Equal([@"C:\a.mp3", @"C:\c.mp3"], Abc.Remove(@"c:\B.mp3").Paths);

    [Fact]
    public void Move_SwapsWithNeighbour_AndStopsAtTheEnds()
    {
        Assert.Equal([@"C:\b.mp3", @"C:\a.mp3", @"C:\c.mp3"], Abc.Move(@"C:\b.mp3", -1).Paths);
        Assert.Equal([@"C:\a.mp3", @"C:\c.mp3", @"C:\b.mp3"], Abc.Move(@"C:\b.mp3", +1).Paths);
        Assert.Same(Abc, Abc.Move(@"C:\a.mp3", -1));
        Assert.Same(Abc, Abc.Move(@"C:\c.mp3", +1));
    }

    [Fact]
    public void MoveBefore_ReordersLikeDragAndDrop()
    {
        Assert.Equal([@"C:\c.mp3", @"C:\a.mp3", @"C:\b.mp3"], Abc.MoveBefore(@"C:\c.mp3", @"C:\a.mp3").Paths);
        Assert.Equal([@"C:\b.mp3", @"C:\a.mp3", @"C:\c.mp3"], Abc.MoveBefore(@"C:\a.mp3", @"C:\c.mp3").Paths);
        Assert.Equal([@"C:\b.mp3", @"C:\c.mp3", @"C:\a.mp3"], Abc.MoveBefore(@"C:\a.mp3", null).Paths);
        Assert.Same(Abc, Abc.MoveBefore(@"C:\a.mp3", @"C:\A.mp3"));
        Assert.Same(Abc, Abc.MoveBefore(@"C:\zzz.mp3", @"C:\a.mp3"));
    }

    [Fact]
    public void SaveThenLoad_KeepsNamesAndOrder()
    {
        var path = Path.Combine(Path.GetTempPath(), $"voxdeck-playlists-{Guid.NewGuid()}.json");
        Playlist.SaveAll(path, [Abc, new Playlist("Memes", [])]);
        var loaded = Playlist.LoadAll(path);
        File.Delete(path);
        Assert.Equal(["Chill", "Memes"], loaded.Select(p => p.Name));
        Assert.Equal(Abc.Paths, loaded[0].Paths);
    }

    [Fact]
    public void MissingFile_GivesNoPlaylists() =>
        Assert.Empty(Playlist.LoadAll(Path.Combine(Path.GetTempPath(), "voxdeck-no-playlists.json")));
}
