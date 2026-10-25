using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace VoxDeck.Core.Capture;

public static class ClipRecorder
{
    /// <summary>
    /// Records a short clip from a recording device, e.g. the virtual cable's microphone side, which is exactly
    /// what Discord hears. Returns stereo float samples and their rate. Nothing is written to disk.
    /// </summary>
    public static Task<(float[] Samples, int SampleRate)> RecordAsync(string deviceId, TimeSpan length)
    {
        var done = new TaskCompletionSource<(float[], int)>();
        var devices = new MMDeviceEnumerator();
        var device = devices.GetDevice(deviceId);
        var capture = new WasapiCapture(device, true, 50);
        var format = capture.WaveFormat;
        var wanted = (int)(length.TotalSeconds * format.SampleRate) * 2;
        var clip = new List<float>(wanted);
        var scratch = new float[format.SampleRate * 2];
        capture.DataAvailable += (_, e) =>
        {
            if (scratch.Length < e.BytesRecorded / format.BlockAlign * 2) scratch = new float[e.BytesRecorded / format.BlockAlign * 2];
            CaptureConvert.ToStereo(e.Buffer, e.BytesRecorded, format, scratch, out var frames);
            clip.AddRange(scratch.AsSpan(0, Math.Min(frames * 2, wanted - clip.Count)));
            if (clip.Count >= wanted) capture.StopRecording();
        };
        capture.RecordingStopped += (_, e) =>
        {
            capture.Dispose();
            device.Dispose();
            devices.Dispose();
            if (e.Exception is not null) done.TrySetException(e.Exception);
            else done.TrySetResult(([.. clip], format.SampleRate));
        };
        capture.StartRecording();
        return done.Task;
    }
}
