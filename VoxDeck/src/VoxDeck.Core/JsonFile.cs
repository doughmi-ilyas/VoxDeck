using System.Text.Json;

namespace VoxDeck.Core;

// Atomic JSON files in the per-user app folder. An unreadable file is kept aside so a later save can't destroy it.
public static class JsonFile
{
    // VOXDECK_DATA points a copy of the app at another folder (UI tests run next to the user's own VoxDeck).
    public static readonly string AppFolder = Environment.GetEnvironmentVariable("VOXDECK_DATA") is { Length: > 0 } folder
        ? folder
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoxDeck");

    public static T? Load<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            File.Copy(path, path + ".corrupt", overwrite: true);
            return null;
        }
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value));
        File.Move(temp, path, overwrite: true);
    }
}
