using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoxDeck.Core.Capture;

// Shared-mode capture with the default stream category; nothing on the device is changed.
// Whether Voice Clarity / vendor processing applies to this stream must be verified on real hardware.
public sealed class MicMeter : IDisposable
{
    readonly MMDevice _device;
    readonly WasapiCapture _capture;
    float _peak;

    /// <summary>Raised when capture stops on its own, e.g. the device was unplugged.</summary>
    public event Action<string>? Failed;

    public MicMeter(string deviceId)
    {
        using var devices = new MMDeviceEnumerator();
        _device = devices.GetDevice(deviceId);
        _capture = new WasapiCapture(_device, true, 50);
        _capture.DataAvailable += OnData;
        _capture.RecordingStopped += (_, e) => { if (e.Exception is not null) Failed?.Invoke(e.Exception.Message); };
        try
        {
            _capture.StartRecording();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>Peak level (0..1) since the previous call.</summary>
    public float TakePeak() => Interlocked.Exchange(ref _peak, 0);

    void OnData(object? sender, WaveInEventArgs e)
    {
        var bytes = e.Buffer.AsSpan(0, e.BytesRecorded);
        var peak = 0f;
        switch (_capture.WaveFormat.BitsPerSample)
        {
            case 32:
                foreach (var s in MemoryMarshal.Cast<byte, float>(bytes)) peak = Math.Max(peak, Math.Abs(s));
                break;
            case 16:
                foreach (var s in MemoryMarshal.Cast<byte, short>(bytes)) peak = Math.Max(peak, Math.Abs(s / 32768f));
                break;
        }
        if (peak > _peak) _peak = peak;
    }

    public void Dispose()
    {
        _capture.Dispose();
        _device.Dispose();
    }
}
