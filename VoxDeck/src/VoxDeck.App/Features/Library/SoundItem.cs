using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

// A library entry plus its on-screen playback state.
public sealed class SoundItem(Sound model) : ViewModel
{
    public Sound Model
    {
        get;
        set
        {
            if (Set(ref field, value)) Raise(nameof(Details));
        }
    } = model;

    /// <summary>The most recent playback started from this sound.</summary>
    public Playback? Playback { get; set; }
    public float[]? Peaks { get; set; }
    public bool IsPlaying { get; set => Set(ref field, value); }
    public bool IsCurrent { get; set => Set(ref field, value); }
    public double Progress { get; set => Set(ref field, value); }

    public string Details
    {
        get
        {
            var parts = new List<string> { Model.Format };
            if (Model.DurationSeconds is { } duration) parts.Add(Time(duration));
            if (Model.StartSeconds > 0) parts.Add($"from {Time(Model.StartSeconds)}");
            return string.Join("  ·  ", parts);
        }
    }

    public static string Time(double seconds)
    {
        var t = TimeSpan.FromSeconds(seconds);
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}
