namespace VoxDeck.Core.Library;

/// <param name="Paths">Sounds in play order, by file path (how the library tells sounds apart).</param>
public sealed record Playlist(string Name, string[] Paths)
{
    public static readonly string DefaultPath = Path.Combine(JsonFile.AppFolder, "playlists.json");

    public static List<Playlist> LoadAll(string path) => JsonFile.Load<List<Playlist>>(path) ?? [];

    public static void SaveAll(string path, IEnumerable<Playlist> playlists) => JsonFile.Save(path, playlists);

    public bool Contains(string soundPath) => Paths.Contains(soundPath, StringComparer.OrdinalIgnoreCase);

    public Playlist Add(string soundPath) => Contains(soundPath) ? this : this with { Paths = [.. Paths, soundPath] };

    public Playlist Remove(string soundPath) => this with { Paths = [.. Paths.Where(p => !string.Equals(p, soundPath, StringComparison.OrdinalIgnoreCase))] };

    /// <summary>Drag-and-drop reorder: puts a sound just before another one (or at the end when <paramref name="beforePath"/> is null).</summary>
    public Playlist MoveBefore(string soundPath, string? beforePath)
    {
        if (!Contains(soundPath) || string.Equals(soundPath, beforePath, StringComparison.OrdinalIgnoreCase)) return this;
        var rest = Remove(soundPath).Paths.ToList();
        var at = beforePath is null ? -1 : rest.FindIndex(p => string.Equals(p, beforePath, StringComparison.OrdinalIgnoreCase));
        rest.Insert(at < 0 ? rest.Count : at, Paths.First(p => string.Equals(p, soundPath, StringComparison.OrdinalIgnoreCase)));
        return this with { Paths = [.. rest] };
    }

    /// <summary>Moves a sound one place earlier (-1) or later (+1); does nothing at the ends.</summary>
    public Playlist Move(string soundPath, int step)
    {
        var from = Array.FindIndex(Paths, p => string.Equals(p, soundPath, StringComparison.OrdinalIgnoreCase));
        var to = from + step;
        if (from < 0 || to < 0 || to >= Paths.Length) return this;
        var paths = Paths.ToArray();
        (paths[from], paths[to]) = (paths[to], paths[from]);
        return this with { Paths = paths };
    }
}
