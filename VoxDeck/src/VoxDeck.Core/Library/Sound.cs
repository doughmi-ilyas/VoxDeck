using System.Text.Json.Serialization;

namespace VoxDeck.Core.Library;

/// <param name="StartSeconds">Where tiles and hotkeys start playback; loops return here too.</param>
public sealed record Sound(
    string Title,
    string Path,
    double StartSeconds = 0,
    double Volume = 1,
    bool Loop = false,
    double? DurationSeconds = null)
{
    [JsonIgnore] public string Format => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
}
