using System.IO;
using System.Windows.Threading;
using VoxDeck.App.Features.Library;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Library;

namespace VoxDeck.App.Features.Player;

// Controls for the selected sound. The UI timer only runs while something is playing.
public sealed class PlayerViewModel : ViewModel
{
    const int WaveformBars = 140;

    readonly SoundEngine? _engine;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<SoundItem> _playing = [];

    public PlayerViewModel(SoundEngine? engine, string? error)
    {
        _engine = engine;
        Error = error;
        _timer.Tick += (_, _) => Tick();
        TogglePlay = new Command(OnTogglePlay);
        Stop = new Command(() => CurrentPlayback?.Stop());
        Seek = new Command(p => SeekTo((double)p!));
        SetStart = new Command(p => SetStartAt(p is double fraction ? fraction * Duration : CurrentPlayback?.Position.TotalSeconds));
        ResetStart = new Command(() => SetStartAt(0));
    }

    /// <summary>Raised when a sound's saved settings change.</summary>
    public event Action? SoundEdited;

    public SoundItem? Current
    {
        get;
        private set
        {
            if (field == value) return;
            if (field is not null) field.IsCurrent = false;
            field = value;
            if (value is not null)
            {
                value.IsCurrent = true;
                LoadWaveform(value);
            }
            Raise("");
        }
    }

    public string? Error { get; private set => Set(ref field, value); }
    public bool HasCurrent => Current is not null;
    public bool IsPlaying => CurrentPlayback is { Paused: false };
    public float[]? Peaks => Current?.Peaks;
    public double Progress => Duration > 0 && CurrentPlayback is { } p ? p.Position.TotalSeconds / Duration : 0;
    public double StartMarker => Duration > 0 ? (Current?.Model.StartSeconds ?? 0) / Duration : 0;
    public bool HasStart => Current?.Model.StartSeconds > 0;
    public string StartText => $"Starts at {SoundItem.Time(Current?.Model.StartSeconds ?? 0)}";

    public string TimeText => Current is null
        ? "Click a sound to play it"
        : $"{SoundItem.Time(CurrentPlayback?.Position.TotalSeconds ?? Current.Model.StartSeconds)} / {SoundItem.Time(Duration)}";

    public double Volume
    {
        get => Current?.Model.Volume ?? 1;
        set
        {
            if (Current is null) return;
            Edit(Current, Current.Model with { Volume = value });
            if (CurrentPlayback is { } p) p.Volume = (float)value;
        }
    }

    public bool Loop
    {
        get => Current?.Model.Loop ?? false;
        set
        {
            if (Current is null) return;
            Edit(Current, Current.Model with { Loop = value });
            if (CurrentPlayback is { } p) p.Loop = value;
        }
    }

    public Command TogglePlay { get; }
    public Command Stop { get; }
    public Command Seek { get; }
    public Command SetStart { get; }
    public Command ResetStart { get; }

    Playback? CurrentPlayback => Current?.Playback is { Ended: false } p ? p : null;
    double Duration => Current?.Playback?.Duration.TotalSeconds ?? Current?.Model.DurationSeconds ?? 0;

    public void Play(SoundItem item, double? fromSeconds = null)
    {
        Current = item;
        Error = null;
        var sound = item.Model;
        if (_engine is null)
        {
            Error = "No sound output device is available.";
            return;
        }
        if (!File.Exists(sound.Path))
        {
            Error = $"\"{sound.Title}\" was moved or deleted: {sound.Path}";
            return;
        }
        try
        {
            var playback = _engine.Play(sound.Path, TimeSpan.FromSeconds(fromSeconds ?? sound.StartSeconds),
                TimeSpan.FromSeconds(sound.StartSeconds), (float)sound.Volume, sound.Loop);
            if (playback is null)
            {
                Error = "Too many sounds are playing. Press Stop all.";
                return;
            }
            item.Playback = playback;
            if (!_playing.Contains(item)) _playing.Add(item);
            if (sound.DurationSeconds is null) Edit(item, sound with { DurationSeconds = playback.Duration.TotalSeconds });
            _timer.Start();
            Tick();
        }
        catch (Exception ex)
        {
            Error = $"Couldn't play \"{sound.Title}\": {ex.Message}";
        }
    }

    public void Forget(SoundItem item)
    {
        if (Current == item) Current = null;
    }

    void OnTogglePlay()
    {
        if (CurrentPlayback is { } p)
        {
            p.Paused = !p.Paused;
            Raise(nameof(IsPlaying));
        }
        else if (Current is not null)
        {
            Play(Current);
        }
    }

    void SeekTo(double fraction)
    {
        if (Current is null || Duration <= 0) return;
        var seconds = Math.Clamp(fraction, 0, 1) * Duration;
        if (CurrentPlayback is { } p)
        {
            p.Position = TimeSpan.FromSeconds(seconds);
            Tick();
        }
        else
        {
            Play(Current, seconds);
        }
    }

    void SetStartAt(double? seconds)
    {
        if (Current is null) return;
        if (seconds is null)
        {
            Error = "Play the sound, then press Set start at the moment it should begin.";
            return;
        }
        Error = null;
        Edit(Current, Current.Model with { StartSeconds = Math.Round(seconds.Value, 2) });
    }

    void Edit(SoundItem item, Sound model)
    {
        item.Model = model;
        SoundEdited?.Invoke();
        if (item == Current) Raise("");
    }

    void Tick()
    {
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            var item = _playing[i];
            if (item.Playback is { Ended: false } p)
            {
                item.IsPlaying = true;
                item.Progress = p.Duration > TimeSpan.Zero ? p.Position / p.Duration : 0;
            }
            else
            {
                item.IsPlaying = false;
                item.Progress = 0;
                item.Playback = null;
                _playing.RemoveAt(i);
            }
        }
        if (_playing.Count == 0) _timer.Stop();
        Raise(nameof(IsPlaying));
        Raise(nameof(Progress));
        Raise(nameof(TimeText));
    }

    async void LoadWaveform(SoundItem item)
    {
        if (item.Peaks is not null) return;
        try
        {
            var (peaks, duration) = await Task.Run(() => Waveform.Load(item.Model.Path, WaveformBars));
            item.Peaks = peaks;
            if (item.Model.DurationSeconds is null) Edit(item, item.Model with { DurationSeconds = duration.TotalSeconds });
            if (item == Current) Raise("");
        }
        catch (Exception)
        {
            // Missing or unreadable files are reported when played; the timeline just stays flat.
        }
    }
}
