using NAudio.Wave;

namespace VoxDeck.Core.Audio;

public static class Waveform
{
    /// <summary>Peak per bucket, normalized to 0..1. Decodes the whole file, so call it off the UI thread.</summary>
    public static (float[] Peaks, TimeSpan Duration) Load(string path, int buckets)
    {
        using var reader = new AudioFileReader(path);
        var totalSamples = Math.Max(1, reader.Length / sizeof(float));
        var perBucket = Math.Max(1, totalSamples / buckets);
        var peaks = new float[buckets];
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
        long index = 0;
        int n;
        while ((n = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < n; i++, index++)
            {
                var bucket = (int)Math.Min(buckets - 1, index / perBucket);
                peaks[bucket] = Math.Max(peaks[bucket], Math.Abs(buffer[i]));
            }
        }

        var max = peaks.Max();
        if (max > 0)
            for (var i = 0; i < peaks.Length; i++) peaks[i] /= max;
        return (peaks, reader.TotalTime);
    }
}
