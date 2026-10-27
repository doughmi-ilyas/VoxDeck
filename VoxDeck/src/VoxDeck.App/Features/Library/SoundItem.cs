using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Library;

// A library entry plus its on-screen state.
public sealed class SoundItem : ViewModel
{
    public SoundItem(Sound model, Action<SoundItem, double> playAt)
    {
        Model = model;
        PlayAt = new Command(p => playAt(this, (double)p!));
    }

    public Sound Model
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            TilePeaks = value.Peaks?.Select(b => b / 255f).ToArray();
            Raise("");
        }
    }

    /// <summary>Plays from a 0..1 position; used by the tile's waveform.</summary>
    public Command PlayAt { get; }

    /// <summary>The most recent playback started from this sound.</summary>
    public Playback? Playback { get; set; }
    public float[]? TilePeaks { get; private set; }
    public float[]? DetailPeaks { get; set; }
    public bool PeaksFailed { get; set; }

    public bool IsPlaying { get; set => Set(ref field, value); }
    public bool IsPaused { get; set => Set(ref field, value); }
    public bool IsSelected { get; set => Set(ref field, value); }
    public bool IsMissing
    {
        get;
        set
        {
            if (Set(ref field, value)) Raise(nameof(Tag));
        }
    }
    public bool HotkeyFailed { get; set => Set(ref field, value); }
    /// <summary>Another sound is being dragged over this one (reordering a playlist).</summary>
    public bool IsDropTarget { get; set => Set(ref field, value); }
    public double Progress { get; set => Set(ref field, value); }

    public string Tag
    {
        get
        {
            if (IsMissing) return "File missing";
            var parts = new List<string> { Model.Format };
            if (Model.Loop) parts.Add("Loop");
            if (Model.StartSeconds > 0) parts.Add($"from {Time(Model.StartSeconds)}");
            return string.Join(" · ", parts);
        }
    }

    public string DurationText => Model.DurationSeconds is { } d ? Time(d) : "–:––";
    public string? HotkeyText => Model.Hotkey is { } h ? Hotkeys.HotkeyText.Format(h) : null;
    public string FavoriteMenuText => Model.Favorite ? "Remove from favorites" : "Add to favorites";

    public static string Time(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return $"{(int)t.TotalMinutes}:{t.Seconds:00}";
    }
}
