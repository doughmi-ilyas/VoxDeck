using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace VoxDeck.Core.Capture;

// Feeds the chosen microphone into the voice-chat mix. Shared-mode capture with the default stream category,
// so Windows' own microphone processing stays as the user set it; nothing on the device is changed.
public sealed class MicInput : IDisposable
{
    static readonly TimeSpan MaxBacklog = TimeSpan.FromMilliseconds(120);

    readonly MMDevice _device;
    readonly WasapiCapture _capture;
    readonly BufferedWaveProvider _buffer;
    readonly VolumeSampleProvider _volume;
    float[] _samples = [];
    byte[] _bytes = [];
    float _peak;
    float _gain = 1;
    bool _muted;

    /// <param name="sampleRate">The rate of the mix this mic goes into; it is resampled if needed.</param>
    public MicInput(string deviceId, int sampleRate, Action<string> failed)
    {
        using var devices = new MMDeviceEnumerator();
        _device = devices.GetDevice(deviceId);
        try
        {
            _capture = new WasapiCapture(_device, true, 20);
            var rate = _capture.WaveFormat.SampleRate;
            _buffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(rate, 2))
            {
                BufferDuration = TimeSpan.FromMilliseconds(500),
                DiscardOnBufferOverflow = true,
                ReadFully = true, // silence instead of ending when the mic hasn't delivered yet
            };
            _volume = new VolumeSampleProvider(_buffer.ToSampleProvider());
            Output = rate == sampleRate ? _volume : new WdlResamplingSampleProvider(_volume, sampleRate);
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += (_, e) =>
            {
                if (e.Exception is null) return;
                Failed = true;
                failed($"{_device.FriendlyName}: {e.Exception.Message}");
            };
            _capture.StartRecording();
        }
        catch
        {
            _capture?.Dispose();
            _device.Dispose();
            throw;
        }
    }

    public string Id => _device.ID;
    public bool Failed { get; private set; }

    /// <summary>Stereo float at the mix rate, after gain and mute.</summary>
    public ISampleProvider Output { get; }

    public float Gain
    {
        get => _gain;
        set
        {
            _gain = value;
            _volume.Volume = _muted ? 0 : value;
        }
    }

    public bool Muted
    {
        get => _muted;
        set
        {
            _muted = value;
            _volume.Volume = value ? 0 : _gain;
        }
    }

    /// <summary>Input level before gain (0..1) since the previous call, so the meter still moves while muted.</summary>
    public float TakePeak() => Interlocked.Exchange(ref _peak, 0);

    void OnData(object? sender, WaveInEventArgs e)
    {
        var frames = e.BytesRecorded / _capture.WaveFormat.BlockAlign;
        if (_samples.Length < frames * 2)
        {
            _samples = new float[frames * 2];
            _bytes = new byte[frames * 2 * sizeof(float)];
        }
        var peak = CaptureConvert.ToStereo(e.Buffer, e.BytesRecorded, _capture.WaveFormat, _samples, out frames);
        if (peak > _peak) _peak = peak;

        // The mic and the call device run on separate clocks; a growing backlog is dropped
        // (one tiny skip) instead of resampling to track drift. Add drift correction if skips are heard.
        if (_buffer.BufferedDuration > MaxBacklog) _buffer.ClearBuffer();
        Buffer.BlockCopy(_samples, 0, _bytes, 0, frames * 2 * sizeof(float));
        _buffer.AddSamples(_bytes, 0, frames * 2 * sizeof(float));
    }

    public void Dispose()
    {
        _capture.Dispose();
        _device.Dispose();
    }
}
