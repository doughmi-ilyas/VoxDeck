using System.Diagnostics;
using System.Windows.Threading;
using VoxDeck.App.Features.Settings;
using VoxDeck.Core.Audio;
using VoxDeck.Core.Capture;
using VoxDeck.Core.Devices;

namespace VoxDeck.App.Features.AudioSetup;

// Device overview. The mic meter only runs while this page is on screen, so Windows' "mic in use" indicator stays off otherwise.
public sealed class AudioSetupViewModel : ViewModel
{
    readonly SettingsViewModel _settings;
    readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    MicMeter? _meter;

    public AudioSetupViewModel(SettingsViewModel settings, SoundEngine? engine)
    {
        _settings = settings;
        _timer.Tick += (_, _) => Level = Math.Max(ToMeterScale(_meter?.TakePeak() ?? 0), Level - 0.04);
        Refresh = new Command(() =>
        {
            LoadDevices();
            if (_timer.IsEnabled) StartMeter();
        });
        TestOutput = new Command(() => engine?.PlayTestTone());
        OpenSoundSettings = new Command(() => Process.Start(new ProcessStartInfo("ms-settings:sound") { UseShellExecute = true }));
        LoadDevices();
    }

    public string Title => "Audio";
    public string Icon => "";
    public string? Badge => null;

    public List<AudioDevice> Microphones { get; private set => Set(ref field, value); } = [];

    public AudioDevice? SelectedMic
    {
        get;
        set
        {
            if (!Set(ref field, value)) return;
            if (value is not null && value.Id != _settings.Current.MicrophoneId) _settings.Update(s => s with { MicrophoneId = value.Id });
            Raise(nameof(MicName));
            Raise(nameof(MicState));
            if (_timer.IsEnabled) StartMeter();
        }
    }

    public string MicName => SelectedMic?.Name ?? "No microphone found";
    public string MicState => SelectedMic is null ? "Missing" : "Ready";
    public string OutputName { get; private set => Set(ref field, value); } = "";
    public bool HasOutput { get; private set => Set(ref field, value); }
    public string MicStatus { get; private set => Set(ref field, value); } = "";
    public double Level { get; private set => Set(ref field, value); }

    public Command Refresh { get; }
    public Command TestOutput { get; }
    public Command OpenSoundSettings { get; }

    public void Start()
    {
        LoadDevices();
        _timer.Start();
        StartMeter();
    }

    public void Stop()
    {
        _timer.Stop();
        StopMeter();
        Level = 0;
    }

    void LoadDevices()
    {
        Microphones = AudioDevices.Microphones();
        var wanted = SelectedMic?.Id ?? _settings.Current.MicrophoneId;
        SelectedMic = Microphones.FirstOrDefault(d => d.Id == wanted)
                      ?? Microphones.FirstOrDefault(d => d.IsDefault)
                      ?? Microphones.FirstOrDefault();
        var output = AudioDevices.DefaultOutput();
        OutputName = output?.Name ?? "No output device found";
        HasOutput = output is not null;
    }

    void StartMeter()
    {
        StopMeter();
        if (SelectedMic is null)
        {
            MicStatus = "No microphone found. Connect one, then press Refresh.";
            return;
        }
        try
        {
            var meter = new MicMeter(SelectedMic.Id);
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
