namespace VoxDeck.Core.Library;

// The library is a JSON list of file references; original audio files are never moved or deleted.
public static class SoundLibrary
{
    public static readonly string DefaultPath = Path.Combine(JsonFile.AppFolder, "library.json");

    public static readonly string[] Extensions = [".wav", ".mp3", ".m4a", ".aac", ".wma", ".flac"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static List<Sound> Load(string path) => JsonFile.Load<List<Sound>>(path) ?? [];

    public static void Save(string path, IEnumerable<Sound> sounds) => JsonFile.Save(path, sounds);
}
