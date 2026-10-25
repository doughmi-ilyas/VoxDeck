using NAudio.Wave;

namespace VoxDeck.Core.Audio;

// Keeps a mix under full scale so stacked sounds don't distort: gain drops at once when a block would clip
// and recovers over about a third of a second. Also records the output peak for meters.
// One gain per block; switch to a per-sample envelope if pumping is ever audible.
internal sealed class Limiter(ISampleProvider source) : ISampleProvider
{
    public const float Ceiling = 0.98f;
    const double ReleaseSeconds = 0.3;

    float _gain = 1;
    float _peak;

    public WaveFormat WaveFormat => source.WaveFormat;

    /// <summary>Highest output level (0..1) since the previous call.</summary>
    public float TakePeak() => Interlocked.Exchange(ref _peak, 0);

    public int Read(float[] buffer, int offset, int count)
    {
        var n = source.Read(buffer, offset, count);
        var peak = 0f;
        for (var i = offset; i < offset + n; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));

        var release = 1 - Math.Exp(-n / (WaveFormat.SampleRate * WaveFormat.Channels * ReleaseSeconds));
        var gain = (float)(_gain + (1 - _gain) * release);
        if (peak * gain > Ceiling) gain = Ceiling / peak;
        if (gain < 0.9999f)
            for (var i = offset; i < offset + n; i++) buffer[i] *= gain;
        _gain = gain;

        var output = peak * gain;
        if (output > _peak) _peak = output;
        return n;
    }
}
