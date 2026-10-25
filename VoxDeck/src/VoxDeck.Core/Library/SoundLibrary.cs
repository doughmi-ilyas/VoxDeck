using System.Text.Json;

namespace VoxDeck.Core.Library;

// The library is a JSON list of file references; original audio files are never moved or deleted.
public static class SoundLibrary
{
    public static readonly string DefaultPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoxDeck", "library.json");

    public static readonly string[] Extensions = [".wav", ".mp3", ".m4a", ".aac", ".wma", ".flac"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static List<Sound> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<Sound>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException)
        {
            // Keep the unreadable file so the next save can't destroy it.
            File.Copy(path, path + ".corrupt", overwrite: true);
            return [];
        }
    }

    public static void Save(string path, IEnumerable<Sound> sounds)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(sounds));
        File.Move(temp, path, overwrite: true);
    }
}
