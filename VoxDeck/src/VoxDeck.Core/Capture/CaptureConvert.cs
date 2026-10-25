using NAudio.Wave;

namespace VoxDeck.Core.Capture;

public static class CaptureConvert
{
    /// <summary>
    /// Turns raw capture bytes (16-bit PCM or 32-bit float, any channel count) into stereo float:
    /// all channels are averaged and the result goes to both sides. Returns the peak level.
    /// </summary>
    public static float ToStereo(byte[] buffer, int bytes, WaveFormat format, float[] destination, out int frames)
    {
        var channels = format.Channels;
        var bytesPerSample = format.BitsPerSample / 8;
        frames = Math.Min(bytes / format.BlockAlign, destination.Length / 2);
        var peak = 0f;
        for (var frame = 0; frame < frames; frame++)
        {
            var sum = 0f;
            for (var channel = 0; channel < channels; channel++)
            {
                var at = (frame * channels + channel) * bytesPerSample;
                sum += bytesPerSample switch
                {
                    4 => BitConverter.ToSingle(buffer, at),
                    2 => BitConverter.ToInt16(buffer, at) / 32768f,
                    _ => 0,
                };
            }
            var mono = sum / channels;
            destination[2 * frame] = destination[2 * frame + 1] = mono;
            peak = Math.Max(peak, Math.Abs(mono));
        }
        return peak;
    }
}
