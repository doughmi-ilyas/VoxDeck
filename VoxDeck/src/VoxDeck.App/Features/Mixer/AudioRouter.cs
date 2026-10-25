using System.Windows.Threading;
using VoxDeck.App.Features.Settings;
using VoxDeck.App.Features.Toasts;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Devices;

namespace VoxDeck.App.Features.Mixer;

// Keeps the engine's devices in line with the settings and with what is actually plugged in.
// Runs at startup, whenever Windows reports a device change, and after a device fails.
public sealed class AudioRouter : ViewModel, IDisposable
{
    readonly SoundEngine _engine;
    readonly SettingsViewModel _settings;
    readonly ToastsViewModel _toasts;
    readonly DeviceWatcher _watcher = new();
    readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    readonly Queue<DateTime> _failures = new();

    public AudioRouter(SoundEngine engine, SettingsViewModel settings, ToastsViewModel toasts)
    {
        _engine = engine;
        _settings = settings;
        _toasts = toasts;
        var dispatcher = Dispatcher.CurrentDispatcher;
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Refresh();
        };
        _watcher.Changed += () => dispatcher.InvokeAsync(ScheduleRefresh);
        engine.Failed += message => dispatcher.InvokeAsync(() => OnDeviceFailed(message));
    }

    /// <summary>Raised after every refresh.</summary>
    public event Action? Changed;

    public List<AudioDevice> Outputs { get; private set; } = [];
    public List<AudioDevice> Microphones { get; private set; } = [];
    public List<VirtualCable> Cables { get; private set; } = [];
    public VirtualCable? Cable { get; private set; }
    public AudioDevice? Microphone { get; private set; }
    public VoiceState State { get; private set; }
    public string? Problem { get; private set; }

    public bool IsRunning => State == VoiceState.Running;
    public string MonitorName => _engine.MonitorName ?? "No output device";
    public string MicName => Microphone?.Name ?? "No microphone found";
    public string MicState => Microphone is null ? "Missing" : "Ready";
    public string VoiceBadge => State switch { VoiceState.Running => "On", VoiceState.Off => "Off", _ => "Setup" };
    public bool IsCableMicrophone(string deviceId) => Cables.Any(c => c.Microphone.Id == deviceId);

    public void Refresh()
    {
        Outputs = AudioDevices.Outputs();
        Microphones = AudioDevices.Microphones();
        Cables = VirtualCables.Find(Outputs, Microphones);
        ApplyMonitor();
        ApplyVoice();
        Raise("");
        Changed?.Invoke();
    }

    void ScheduleRefresh()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    void ApplyMonitor()
    {
        var wanted = Outputs.FirstOrDefault(o => o.Id == _settings.Current.MonitorDeviceId) ?? Outputs.FirstOrDefault(o => o.IsDefault);
        try
        {
            _engine.SetMonitorDevice(wanted?.Id);
        }
        catch (Exception ex)
        {
            _engine.SetMonitorDevice(null);
            _toasts.Error($"Can't open “{wanted?.Name}”: {ex.Message}");
        }
    }

    void ApplyVoice()
    {
        var settings = _settings.Current;
        (State, Cable, Microphone) = VoiceRouting.Choose(settings.VoiceMixEnabled, Cables, Microphones, settings.CableDeviceId, settings.MicrophoneId);
        Problem = null;
        if (State != VoiceState.Running)
        {
            _engine.StopVoiceMix();
            return;
        }
        var cable = Cable!.Speaker.Id;
        var mic = Microphone!.Id;
        if (_engine.CallDeviceId == cable && _engine.MicDeviceId == mic && !_engine.VoiceMixFailed) return;
        try
        {
            _engine.StartVoiceMix(cable, mic);
        }
        catch (Exception ex)
        {
            _engine.StopVoiceMix();
            State = VoiceState.Failed;
            Problem = ex.Message;
        }
    }

    // Retry automatically, but stop after a few failures in a row so a broken driver can't spam errors.
    void OnDeviceFailed(string message)
    {
        _toasts.Error($"Audio device stopped. {message}");
        var now = DateTime.UtcNow;
        _failures.Enqueue(now);
        while (_failures.Peek() < now.AddSeconds(-20)) _failures.Dequeue();
        if (_failures.Count <= 3) ScheduleRefresh();
    }

    public void Dispose() => _watcher.Dispose();
}
