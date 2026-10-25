using System.Windows.Threading;
using VoxDeck.Core.Capture;
using VoxDeck.Core.Devices;

namespace VoxDeck.App.Features.AudioSetup;

// The mic meter only runs while this page is on screen.
public sealed class AudioSetupViewModel : ViewModel
{
    readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    MicMeter? _meter;

    public AudioSetupViewModel()
    {
        _timer.Tick += (_, _) => Level = Math.Max(ToMeterScale(_meter?.TakePeak() ?? 0), Level - 0.04);
        Refresh = new Command(() =>
        {
            LoadDevices();
            StartMeter();
        });
    }

    public string Title => "Audio setup";
    public string Icon => "";
    public List<AudioDevice> Microphones { get; private set => Set(ref field, value); } = [];

    public AudioDevice? SelectedMic
    {
        get;
        set
        {
            if (Set(ref field, value) && _timer.IsEnabled) StartMeter();
        }
    }

    public string DefaultOutput { get; private set => Set(ref field, value); } = "";
    public string MicStatus { get; private set => Set(ref field, value); } = "";
    public double Level { get; private set => Set(ref field, value); }

    public Command Refresh { get; }

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
        SelectedMic = Microphones.FirstOrDefault(d => d.Id == SelectedMic?.Id)
                      ?? Microphones.FirstOrDefault(d => d.IsDefault)
                      ?? Microphones.FirstOrDefault();
        DefaultOutput = AudioDevices.DefaultOutput()?.Name ?? "No output device found";
    }

    void StartMeter()
    {
        StopMeter();
        if (SelectedMic is null)
        {
            MicStatus = "No microphone found. Connect one, then click Refresh.";
            return;
        }
        try
        {
            var meter = new MicMeter(SelectedMic.Id);
            meter.Failed += _ => _dispatcher.InvokeAsync(() =>
            {
                if (_meter == meter) MicStatus = "Disconnected. Pick another microphone or click Refresh.";
            });
            _meter = meter;
            MicStatus = "Connected. Speak and watch the level move.";
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
