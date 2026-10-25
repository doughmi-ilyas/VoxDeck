using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxDeck.Core.Audio;

// One open output device. Sounds → sound volume → main mix (+ extra inputs such as the mic) → limiter → WASAPI shared mode.
internal sealed class Output : IDisposable
{
    readonly MMDevice _device;
    readonly WasapiOut _out;
    readonly MixingSampleProvider _sounds;
    readonly VolumeSampleProvider _soundVolume;
    readonly MixingSampleProvider _main;
    readonly Limiter _limiter;
    int _soundCount;

    public Output(string deviceId, Action<string> failed)
    {
        using var devices = new MMDeviceEnumerator();
        _device = devices.GetDevice(deviceId);
        try
        {
            Name = _device.FriendlyName;
            Format = WaveFormat.CreateIeeeFloatWaveFormat(_device.AudioClient.MixFormat.SampleRate, 2);
            // Both mixers read fully (silence when empty) so the stream and the mic never get dropped.
            _sounds = new MixingSampleProvider(Format) { ReadFully = true };
            _sounds.MixerInputEnded += (_, e) =>
            {
                Interlocked.Decrement(ref _soundCount);
                (e.SampleProvider as IDisposable)?.Dispose();
            };
            _soundVolume = new VolumeSampleProvider(_sounds);
            _main = new MixingSampleProvider(Format) { ReadFully = true };
            _main.AddMixerInput(_soundVolume);
            _limiter = new Limiter(_main);
            _out = new WasapiOut(_device, AudioClientShareMode.Shared, true, 50);
            _out.PlaybackStopped += (_, e) =>
            {
                if (e.Exception is null) return;
                Failed = true;
                failed($"{Name}: {e.Exception.Message}");
            };
            _out.Init(_limiter);
            _out.Play();
        }
        catch
        {
            _device.Dispose();
            throw;
        }
    }

    public string Id => _device.ID;
    public string Name { get; }
    public WaveFormat Format { get; }
    public bool Failed { get; private set; }
    public int SoundCount => Volatile.Read(ref _soundCount);

    public float SoundVolume
    {
        get => _soundVolume.Volume;
        set => _soundVolume.Volume = value;
    }

    public void AddSound(ISampleProvider voice)
    {
        Interlocked.Increment(ref _soundCount);
        _sounds.AddMixerInput(voice);
    }

    public void AddInput(ISampleProvider input) => _main.AddMixerInput(input);

    public float TakePeak() => _limiter.TakePeak();

    public void Dispose()
    {
        _out.Dispose();
        foreach (var voice in _sounds.MixerInputs.OfType<IDisposable>()) voice.Dispose(); // the stream is stopped, so this is safe
        _device.Dispose();
    }
}
