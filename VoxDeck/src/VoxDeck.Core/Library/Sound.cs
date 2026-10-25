using System.Text.Json.Serialization;
using VoxDeck.Core.Hotkeys;

namespace VoxDeck.Core.Library;

/// <param name="StartSeconds">Where tiles and hotkeys start playback; loops return here too.</param>
/// <param name="Peaks">Small waveform for the tile, one byte (0..255) per bar.</param>
public sealed record Sound(
    string Title,
    string Path,
    double StartSeconds = 0,
    double Volume = 1,
    bool Loop = false,
    double? DurationSeconds = null,
    bool Favorite = false,
    Hotkey? Hotkey = null,
    byte[]? Peaks = null,
    DateTime? LastPlayed = null)
{
    [JsonIgnore] public string Format => System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
}
