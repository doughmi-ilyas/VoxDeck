using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using VoxDeck.App.Features.Player;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Capture;
using VoxDeck.Core.Devices;

namespace VoxDeck.App.Features.Mixer;

// Voice mixing: your mic plus your sounds, sent into a virtual cable that Discord and games use as a microphone.
public sealed class MixerViewModel : ViewModel, IPage
{
    const string CableDownloadPage = "https://vb-audio.com/Cable/";

    readonly SoundEngine _engine;
    readonly SettingsViewModel _settings;
    readonly PlayerViewModel _player;
    readonly ToastsViewModel _toasts;
    readonly DispatcherTimer _meters = new() { Interval = TimeSpan.FromMilliseconds(33) };
    bool _pageVisible;

    public MixerViewModel(SoundEngine engine, AudioRouter router, SettingsViewModel settings, PlayerViewModel player, ToastsViewModel toasts)
    {
        _engine = engine;
        Router = router;
        _settings = settings;
        _player = player;
        _toasts = toasts;
        engine.MicGain = (float)settings.Current.MicGain;
        engine.MicMuted = settings.Current.MicMuted;
        engine.CallSoundVolume = (float)settings.Current.CallSoundVolume;
        _meters.Tick += (_, _) => UpdateMeters();
        router.Changed += () =>
        {
            Raise("");
            UpdateMeterTimer();
        };
        player.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlayerViewModel.MasterVolume)) Raise(nameof(MasterVolume));
        };
        ToggleMute = new Command(() => SetMuted(!MicMuted, announce: false));
        CheckAgain = new Command(OnCheckAgain);
        GetCable = new Command(() => Process.Start(new ProcessStartInfo(CableDownloadPage) { UseShellExecute = true }));
        CopyMicName = new Command(OnCopyMicName);
        RecordTest = new Command(OnRecordTest);
    }

    public string Title => "Mixer";
    public string Icon => "";
    public string? Badge => Router.IsRunning ? "On" : null;

    public AudioRouter Router { get; }

    public bool Enabled
    {
        get => _settings.Current.VoiceMixEnabled;
        set
        {
            if (value == Enabled) return;
            _settings.Update(s => s with { VoiceMixEnabled = value });
            Router.Refresh();
            if (value && Router.IsRunning)
                _toasts.Success($"Voice mixing is on. In Discord, pick “{Router.Cable!.Microphone.Name}” as your microphone.");
        }
    }

    public bool HasCable => Router.Cables.Count > 0;
    public bool HasSeveralCables => Router.Cables.Count > 1;
    public List<VirtualCable> Cables => Router.Cables;

    public VirtualCable? SelectedCable
    {
        get => Router.Cable;
        set
        {
            if (value is null || value == Router.Cable) return;
            _settings.Update(s => s with { CableDeviceId = value.Speaker.Id });
            Router.Refresh();
        }
    }

    public string CableMicName => Router.Cable?.Microphone.Name ?? "CABLE Output (VB-Audio Virtual Cable)";
    public string CableSpeakerName => Router.Cable?.Speaker.Name ?? "Virtual cable";

    public string StatusText => Router.State switch
    {
        VoiceState.Running => $"Live: your microphone and sounds are going into “{Router.Cable!.Speaker.Name}”.",
        VoiceState.Off when HasCable => "Off. Turn on “Send to voice chat” when you're ready.",
        VoiceState.Off or VoiceState.NoCable => "Needs a virtual audio cable. Setup takes about two minutes.",
        VoiceState.NoMic => "No microphone found. Connect one, then press Check again.",
        VoiceState.CableAsMic => "Your microphone is set to the virtual cable itself. Pick your real microphone on the Audio page.",
        _ => $"Couldn't start voice mixing: {Router.Problem}",
    };

    public string StatusKind => Router.State switch
    {
        VoiceState.Running => "ok",
        VoiceState.NoMic or VoiceState.CableAsMic or VoiceState.Failed => "error",
        _ => "info",
    };

    public double MicGain
    {
        get => _settings.Current.MicGain;
        set
        {
            _settings.Update(s => s with { MicGain = value });
            _engine.MicGain = (float)value;
            Raise(nameof(MicGain));
        }
    }

    public bool MicMuted
    {
        get => _settings.Current.MicMuted;
        set => SetMuted(value, announce: false);
    }

    public double CallSoundVolume
    {
        get => _settings.Current.CallSoundVolume;
        set
        {
            _settings.Update(s => s with { CallSoundVolume = value });
            _engine.CallSoundVolume = (float)value;
            Raise(nameof(CallSoundVolume));
        }
    }

    public double MasterVolume
    {
        get => _player.MasterVolume;
        set => _player.MasterVolume = value;
    }

    public double MicLevel { get; private set => Set(ref field, value); }
    public double CallLevel { get; private set => Set(ref field, value); }
    public double MonitorLevel { get; private set => Set(ref field, value); }

    public bool IsTesting
    {
        get;
        private set
        {
            if (Set(ref field, value)) Raise(nameof(CanTest));
        }
    }

    public bool CanTest => !IsTesting;
    public string TestStatus { get; private set => Set(ref field, value); } = "Records five seconds from the virtual cable, then plays it back to you.";

    public Command ToggleMute { get; }
    public Command CheckAgain { get; }
    public Command GetCable { get; }
    public Command CopyMicName { get; }
    public Command RecordTest { get; }

    /// <summary>For the mute shortcut: confirms with a toast because you may be in another app.</summary>
    public void ToggleMuteFromHotkey() => SetMuted(!MicMuted, announce: true);

    public void SetPageVisible(bool visible)
    {
        _pageVisible = visible;
        UpdateMeterTimer();
    }

    void SetMuted(bool muted, bool announce)
    {
        _settings.Update(s => s with { MicMuted = muted });
        _engine.MicMuted = muted;
        Raise(nameof(MicMuted));
        if (announce) _toasts.Info(muted ? "Microphone muted." : "Microphone on.");
    }

    // Meters run while this page is open, or all the time while mixing so the player's mic button stays live.
    void UpdateMeterTimer()
    {
        if (_pageVisible || Router.IsRunning)
        {
            _meters.Start();
            return;
        }
        _meters.Stop();
        MicLevel = CallLevel = MonitorLevel = 0;
    }

    void UpdateMeters()
    {
        MicLevel = Decay(MicLevel, _engine.TakeMicPeak());
        CallLevel = Decay(CallLevel, _engine.TakeCallPeak());
        MonitorLevel = Decay(MonitorLevel, _engine.TakeMonitorPeak());
    }

    // Peak 0..1 shown on a -60..0 dB scale, falling back smoothly.
    static double Decay(double current, float peak)
    {
        var level = peak < 0.001f ? 0 : (20 * Math.Log10(peak) + 60) / 60;
        return Math.Max(level, current - 0.035);
    }

    void OnCheckAgain()
    {
        var before = Router.Cables.Count;
        Router.Refresh();
        if (Router.Cables.Count > before) _toasts.Success($"Found “{Router.Cables[0].Speaker.Name}”. Turn on “Send to voice chat”.");
        else if (Router.Cables.Count == 0) _toasts.Info("Still no virtual cable. If you just installed it, restart Windows first.");
    }

    void OnCopyMicName()
    {
        try
        {
            Clipboard.SetText(CableMicName);
            _toasts.Success("Copied. Paste it into Discord's input device search.");
        }
        catch (Exception)
        {
            _toasts.Error("Couldn't copy right now. Another app is holding the clipboard.");
        }
    }

    async void OnRecordTest()
    {
        if (IsTesting || Router.Cable is not { } cable) return;
        if (!Router.IsRunning)
        {
            _toasts.Info("Turn on “Send to voice chat” first.");
            return;
        }
        IsTesting = true;
        try
        {
            TestStatus = "Recording… talk and play a sound now.";
            var (samples, rate) = await ClipRecorder.RecordAsync(cable.Microphone.Id, TimeSpan.FromSeconds(5));
            TestStatus = "Playing back exactly what your voice chat hears…";
            _engine.PlayClip(samples, rate);
            await Task.Delay(5300);
            TestStatus = "Done. If you heard your voice and your sounds, Discord will hear them too.";
        }
        catch (Exception ex)
        {
            TestStatus = $"The test couldn't record: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }
}
