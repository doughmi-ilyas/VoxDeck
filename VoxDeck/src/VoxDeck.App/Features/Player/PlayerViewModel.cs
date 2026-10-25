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

    public PlayerViewModel(SoundEngine engine, SettingsViewModel settings, ToastsViewModel toasts)
    {
        _engine = engine;
        _settings = settings;
        _toasts = toasts;
        engine.MasterVolume = (float)settings.Current.MasterVolume;
        _timer.Tick += (_, _) => Tick();
        StopAll = new Command(StopEverything);
    }

    /// <summary>Raised after a sound starts, so the library can record when it was last played.</summary>
    public event Action<SoundItem>? Started;
    /// <summary>Raised about 30 times a second while anything plays.</summary>
    public event Action? Ticked;

    public SoundItem? NowPlaying { get; private set => Set(ref field, value); }
    public bool AnyPlaying => _playing.Count > 0;
    public double NowProgress => NowPlaying?.Progress ?? 0;
    public string NowTitle => NowPlaying?.Model.Title ?? "Nothing playing";

    public string NowSubtitle => NowPlaying?.Playback is { Ended: false } p
        ? $"{(p.Paused ? "Paused" : SoundItem.Time(p.Position.TotalSeconds))} / {SoundItem.Time(p.Duration.TotalSeconds)}"
          + (_playing.Count > 1 ? $"  ·  {_playing.Count} sounds playing" : "")
        : "Click a sound or press its hotkey";

    public string OutputName { get; set => Set(ref field, value); } = "";

    public double MasterVolume
    {
        get => _settings.Current.MasterVolume;
        set
        {
            _settings.Update(s => s with { MasterVolume = value });
            _engine.MasterVolume = (float)value;
            Raise(nameof(MasterVolume));
        }
    }

    public Command StopAll { get; }

    public void Play(SoundItem item, double? fromSeconds = null)
    {
        var sound = item.Model;
        if (!File.Exists(sound.Path))
        {
            item.IsMissing = true;
            _toasts.Error($"“{sound.Title}” was moved or deleted.");
            return;
        }
        if (!_settings.Current.OverlapSounds) _engine.StopAll();
        try
        {
            var playback = _engine.Play(sound.Path, TimeSpan.FromSeconds(fromSeconds ?? sound.StartSeconds),
                TimeSpan.FromSeconds(sound.StartSeconds), (float)sound.Volume, sound.Loop, sound.Route);
            if (playback is null)
            {
                _toasts.Error("Too many sounds are playing. Press Stop all.");
                return;
            }
            item.Playback = playback;
            _playing.Remove(item);
            _playing.Add(item);
            NowPlaying = item;
            _timer.Start();
            Started?.Invoke(item);
            Tick();
        }
        catch (InvalidOperationException ex)
        {
            _toasts.Error(ex.Message); // nowhere to play it; the message says what to change
        }
        catch (Exception ex)
        {
            _toasts.Error($"Couldn't play “{sound.Title}”: {ex.Message}");
        }
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

    public void StopEverything() => _engine.StopAll();

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
        if (_playing.Count == 0) _timer.Stop();
        Raise(nameof(AnyPlaying));
        Raise(nameof(NowProgress));
        Raise(nameof(NowTitle));
        Raise(nameof(NowSubtitle));
        Ticked?.Invoke();
    }
}
