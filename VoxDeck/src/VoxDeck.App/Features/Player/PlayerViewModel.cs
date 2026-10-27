using System.IO;
using System.Windows.Threading;
using VoxDeck.App.Features.Library;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.Core.Audio;

namespace VoxDeck.App.Features.Player;

// Starts sounds and tracks what is playing. The UI timer only runs while something plays.
public sealed class PlayerViewModel : ViewModel
{
    readonly SoundEngine _engine;
    readonly SettingsViewModel _settings;
    readonly ToastsViewModel _toasts;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    readonly List<SoundItem> _playing = [];
    // A playlist being played one sound after another; null when sounds are just pressed.
    List<SoundItem>? _queue;
    List<SoundItem> _queueInOrder = []; // the playlist's own order, to come back to when shuffle turns off
    int _queueIndex;
    string _queueName = "";
    double _volumeBeforeMute = 1;

    public PlayerViewModel(SoundEngine engine, SettingsViewModel settings, ToastsViewModel toasts)
    {
        _engine = engine;
        _settings = settings;
        _toasts = toasts;
        engine.MasterVolume = (float)settings.Current.MasterVolume;
        _timer.Tick += (_, _) => Tick();
        StopAll = new Command(StopEverything);
        PauseResume = new Command(() => { if (NowPlaying is { Playback.Ended: false } item) Toggle(item); });
        Next = new Command(OnNext);
        Previous = new Command(OnPrevious);
        ToggleMute = new Command(OnToggleMute);
    }

    /// <summary>Raised after a sound starts, so the library can record when it was last played.</summary>
    public event Action<SoundItem>? Started;
    /// <summary>Raised about 30 times a second while anything plays.</summary>
    public event Action? Ticked;

    public SoundItem? NowPlaying { get; private set => Set(ref field, value); }
    public bool AnyPlaying => _playing.Count > 0;
    public double NowProgress => NowPlaying?.Progress ?? 0;
    public string NowTitle => NowPlaying?.Model.Title ?? "Nothing playing";

    public string NowSubtitle => NowPlaying?.Playback is not { Ended: false } p ? "Click a sound or press its hotkey"
        : (p.Paused ? "Paused  ·  " : "")
          + (_queue is { } q ? $"{_queueName}  ·  {_queueIndex + 1} of {q.Count}"
             : _playing.Count > 1 ? $"{_playing.Count} sounds playing" : "Sound");

    /// <summary>0..1 through the sound shown in the bar. Setting it seeks (the bar's slider).</summary>
    public double Position
    {
        get => NowProgress;
        set
        {
            if (NowPlaying is { Playback.Ended: false } item) Seek(item, value);
        }
    }

    public string PositionText => NowPlaying?.Playback is { Ended: false } p ? SoundItem.Time(p.Position.TotalSeconds) : "0:00";
    public string DurationText => NowPlaying?.Playback is { Ended: false } p ? SoundItem.Time(p.Duration.TotalSeconds) : "0:00";

    /// <summary>Playlists play in random order (the current sound keeps playing).</summary>
    public bool Shuffle
    {
        get;
        set
        {
            if (!Set(ref field, value) || _queue is not { } queue) return;
            var current = queue[_queueIndex];
            var rest = _queueInOrder.Where(i => i != current);
            _queue = value ? [current, .. rest.OrderBy(_ => Random.Shared.Next())] : _queueInOrder;
            _queueIndex = _queue.IndexOf(current);
            Raise(nameof(NowSubtitle));
        }
    }

    /// <summary>Playlists start over at the end; a single sound loops.</summary>
    public bool Repeat
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            if (_queue is null && NowPlaying is { Playback: { Ended: false } p } item) p.Loop = value || item.Model.Loop;
        }
    }

    public bool IsSilent => MasterVolume == 0;

    public bool CanPause => NowPlaying is { Playback.Ended: false };
    public bool NowPaused => NowPlaying is { IsPaused: true };
    /// <summary>A playlist is playing, so next/previous make sense.</summary>
    public bool InQueue => _queue is not null;

    public double MasterVolume
    {
        get => _settings.Current.MasterVolume;
        set
        {
            _settings.Update(s => s with { MasterVolume = value });
            _engine.MasterVolume = (float)value;
            Raise(nameof(MasterVolume));
            Raise(nameof(IsSilent));
        }
    }

    public Command StopAll { get; }
    public Command PauseResume { get; }
    public Command Next { get; }
    public Command Previous { get; }
    public Command ToggleMute { get; }

    public void Play(SoundItem item, double? fromSeconds = null) => Start(item, fromSeconds, fromQueue: false);

    /// <summary>Plays sounds one after another, beginning with <paramref name="first"/> (or the first one).</summary>
    public void PlayAll(IReadOnlyList<SoundItem> items, string name, SoundItem? first = null, bool shuffle = false)
    {
        if (items.Count == 0) return;
        StopEverything();
        if (shuffle) Shuffle = true;
        _queueInOrder = [.. items];
        var start = first ?? (Shuffle ? null : items[0]);
        var rest = _queueInOrder.Where(i => i != start);
        _queue = Shuffle ? [.. new[] { start }.OfType<SoundItem>(), .. rest.OrderBy(_ => Random.Shared.Next())]
            : _queueInOrder;
        _queueName = name;
        Raise(nameof(InQueue));
        PlayFromQueue(start is null ? 0 : _queue.IndexOf(start));
    }

    bool Start(SoundItem item, double? fromSeconds, bool fromQueue)
    {
        var sound = item.Model;
        if (!File.Exists(sound.Path))
        {
            item.IsMissing = true;
            _toasts.Error($"“{sound.Title}” was moved or deleted.");
            return false;
        }
        if (!_settings.Current.OverlapSounds)
        {
            if (!fromQueue) EndQueue(); // pressing another sound takes over from the playlist
            _engine.StopAll();
        }
        try
        {
            // In a playlist each sound plays once, even if it's set to loop, so the list keeps moving.
            var playback = _engine.Play(sound.Path, TimeSpan.FromSeconds(fromSeconds ?? sound.StartSeconds),
                TimeSpan.FromSeconds(sound.StartSeconds), (float)sound.Volume, (sound.Loop || Repeat) && !fromQueue, _settings.Current.SoundRoute);
            if (playback is null)
            {
                _toasts.Error("Too many sounds are playing. Press Stop all.");
                return false;
            }
            item.Playback = playback;
            _playing.Remove(item);
            _playing.Add(item);
            NowPlaying = item;
            _timer.Start();
            Started?.Invoke(item);
            Tick();
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _toasts.Error(ex.Message); // nowhere to play it; the message says what to change
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't play “{sound.Title}”: {ex.Message}");
        }
        return false;
    }

    // Skips sounds that can't play (missing file), so one bad file doesn't end the playlist.
    // With repeat on, the end wraps to the start; trying each sound once at most keeps it from spinning.
    void PlayFromQueue(int index)
    {
        if (_queue is not { } queue) return;
        for (var tries = 0; tries < queue.Count; tries++, index++)
        {
            if (index >= queue.Count)
            {
                if (!Repeat) break;
                index = 0;
            }
            _queueIndex = index;
            if (Start(queue[index], null, fromQueue: true)) return;
        }
        EndQueue();
    }

    void OnNext()
    {
        if (_queue is not { } queue) return;
        queue[_queueIndex].Playback?.Stop();
        PlayFromQueue(_queueIndex + 1);
    }

    // Like Spotify: a few seconds in, "previous" restarts the sound; otherwise it goes back one.
    void OnPrevious()
    {
        if (NowPlaying is { Playback: { Ended: false } p } item && (p.Position.TotalSeconds > 3 || _queue is null))
        {
            p.Position = TimeSpan.FromSeconds(item.Model.StartSeconds);
            Tick();
        }
        else if (_queue is { } queue)
        {
            queue[_queueIndex].Playback?.Stop();
            PlayFromQueue(Math.Max(0, _queueIndex - 1));
        }
    }

    void OnToggleMute()
    {
        if (MasterVolume > 0)
        {
            _volumeBeforeMute = MasterVolume;
            MasterVolume = 0;
        }
        else
        {
            MasterVolume = _volumeBeforeMute > 0 ? _volumeBeforeMute : 0.8;
        }
    }

    void EndQueue()
    {
        if (_queue is null) return;
        _queue = null;
        Raise(nameof(InQueue));
        Raise(nameof(NowSubtitle));
    }

    /// <summary>Pause/resume when it's playing; otherwise start it.</summary>
    public void Toggle(SoundItem item)
    {
        if (item.Playback is { Ended: false } p)
        {
            p.Paused = !p.Paused;
            Tick();
        }
        else
        {
            Play(item);
        }
    }

    /// <summary>Jump to a 0..1 position, starting the sound there if it isn't playing.</summary>
    public void Seek(SoundItem item, double fraction)
    {
        var duration = item.Playback?.Duration.TotalSeconds ?? item.Model.DurationSeconds ?? 0;
        var seconds = Math.Clamp(fraction, 0, 1) * duration;
        if (item.Playback is { Ended: false } p)
        {
            p.Position = TimeSpan.FromSeconds(seconds);
            Tick();
        }
        else
        {
            Play(item, duration > 0 ? seconds : null);
        }
    }

    public void StopEverything()
    {
        EndQueue();
        _engine.StopAll();
    }

    void Tick()
    {
        for (var i = _playing.Count - 1; i >= 0; i--)
        {
            var item = _playing[i];
            if (item.Playback is { Ended: false } p)
            {
                item.IsPlaying = true;
                item.IsPaused = p.Paused;
                item.Progress = p.Duration > TimeSpan.Zero ? p.Position / p.Duration : 0;
            }
            else
            {
                item.IsPlaying = false;
                item.IsPaused = false;
                item.Progress = 0;
                item.Playback = null;
                _playing.RemoveAt(i);
            }
        }
        if (NowPlaying is { IsPlaying: false }) NowPlaying = _playing.LastOrDefault();
        // The playlist's sound finished: on to the next one.
        if (_queue is { } queue && queue[_queueIndex].Playback is null) PlayFromQueue(_queueIndex + 1);
        if (_playing.Count == 0) _timer.Stop();
        Raise(nameof(AnyPlaying));
        Raise(nameof(CanPause));
        Raise(nameof(NowPaused));
        Raise(nameof(Position));
        Raise(nameof(PositionText));
        Raise(nameof(DurationText));
        Raise(nameof(NowProgress));
        Raise(nameof(NowTitle));
        Raise(nameof(NowSubtitle));
        Ticked?.Invoke();
    }
}
