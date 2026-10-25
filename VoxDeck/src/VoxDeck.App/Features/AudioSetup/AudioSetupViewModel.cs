using System.Diagnostics;
using System.Windows.Threading;
using VoxDeck.App.Features.Mixer;
using VoxDeck.App.Features.Settings;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Capture;
using VoxDeck.Core.Devices;

namespace VoxDeck.App.Features.AudioSetup;

/// <param name="Id">Null means "follow the Windows default".</param>
public sealed record DeviceChoice(string? Id, string Name, string Detail, bool IsWarning = false);

// Device choices. The mic meter only runs while this page is on screen, so Windows' "mic in use"
// indicator stays off otherwise (unless voice mixing is on, which needs the mic anyway).
public sealed class AudioSetupViewModel : ViewModel, IPage
{
    readonly SettingsViewModel _settings;
    readonly AudioRouter _router;
    readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    MicMeter? _meter;
    bool _loading;

    public AudioSetupViewModel(SettingsViewModel settings, SoundEngine engine, AudioRouter router)
    {
        _settings = settings;
        _router = router;
        _timer.Tick += (_, _) => Level = Math.Max(ToMeterScale(_meter?.TakePeak() ?? 0), Level - 0.04);
        router.Changed += LoadChoices;
        Refresh = new Command(router.Refresh);
        TestOutput = new Command(engine.PlayTestTone);
        OpenSoundSettings = new Command(() => Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }));
    }

    public string Title => "Audio";
    public string Icon => "";
    public string? Badge => null;

    public List<DeviceChoice> Microphones { get; private set => Set(ref field, value); } = [];
    public List<DeviceChoice> Outputs { get; private set => Set(ref field, value); } = [];

    public DeviceChoice? SelectedMic
    {
        get;
        set
        {
            // The list control briefly reports null while its items are replaced; that's not a choice.
            if (value is null || !Set(ref field, value) || _loading) return;
            _settings.Update(s => s with { MicrophoneId = value.Id });
            _router.Refresh();
            if (_timer.IsEnabled) StartMeter();
        }
    }

    public DeviceChoice? SelectedOutput
    {
        get;
        set
        {
            if (value is null || !Set(ref field, value) || _loading) return;
            _settings.Update(s => s with { MonitorDeviceId = value.Id });
            _router.Refresh();
        }
    }

    public string OutputName => _router.MonitorName;
    public string MicStatus { get; private set => Set(ref field, value); } = "";
    public double Level { get; private set => Set(ref field, value); }

    public Command Refresh { get; }
    public Command TestOutput { get; }
    public Command OpenSoundSettings { get; }

    public void Start()
    {
        _router.Refresh();
        _timer.Start();
        StartMeter();
    }

    public void Stop()
    {
        _timer.Stop();
        StopMeter();
        Level = 0;
    }

    void LoadChoices()
    {
        _loading = true;
        Microphones = _router.Microphones
            .Select(m => _router.IsCableMicrophone(m.Id)
                ? new DeviceChoice(m.Id, m.Name, "Virtual cable: this is what Discord listens to, not your voice", true)
                : new DeviceChoice(m.Id, m.Name, m.IsDefault ? "Windows default" : ""))
            .ToList();
        SelectedMic = Microphones.FirstOrDefault(c => c.Id == _router.Microphone?.Id);

        var windowsDefault = _router.Outputs.FirstOrDefault(o => o.IsDefault);
        Outputs = [new DeviceChoice(null, "Windows default", windowsDefault?.Name ?? "None"),
            .. _router.Outputs.Where(o => !VirtualCables.LooksVirtual(o.Name)).Select(o => new DeviceChoice(o.Id, o.Name, ""))];
        SelectedOutput = Outputs.FirstOrDefault(c => c.Id == _settings.Current.MonitorDeviceId) ?? Outputs[0];
        _loading = false;
        Raise(nameof(OutputName));
        if (_timer.IsEnabled && _meter is null) StartMeter();
    }

    void StartMeter()
    {
        StopMeter();
        if (SelectedMic?.Id is not { } id)
        {
            MicStatus = "No microphone found. Connect one, then press Refresh.";
            return;
        }
        try
        {
            var meter = new MicMeter(id);
            meter.Failed += _ => _dispatcher.InvokeAsync(() =>
            {
                if (_meter == meter) MicStatus = "This microphone was disconnected. Pick another one or press Refresh.";
            });
            _meter = meter;
            MicStatus = "Listening. Say something and watch the meter move.";
        }
        catch (Exception ex)
        {
            MicStatus = $"Can't open this microphone: {ex.Message}";
        }
    }

    void StopMeter()
    {
        _meter?.Dispose();
        _meter = null;
    }

    // Peak 0..1 mapped to a -60..0 dB bar, which tracks speech better than a linear scale.
    static double ToMeterScale(float peak) => peak < 0.001f ? 0 : (20 * Math.Log10(peak) + 60) / 60;
}
