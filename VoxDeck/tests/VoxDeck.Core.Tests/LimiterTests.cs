using NAudio.Wave;
using VoxDeck.Core.Audio;

namespace VoxDeck.Core.Tests;

public class LimiterTests
{
    static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

    [Fact]
    public void LoudMix_NeverExceedsTheCeiling()
    {
        var limiter = new Limiter(new Constant(3.0f));
        var buffer = new float[960];
        for (var block = 0; block < 50; block++)
        {
            limiter.Read(buffer, 0, buffer.Length);
            Assert.All(buffer, s => Assert.True(Math.Abs(s) <= Limiter.Ceiling + 1e-6, $"sample {s} clipped"));
        }
    }

    [Fact]
    public void QuietMix_PassesThroughUnchanged()
    {
        var limiter = new Limiter(new Constant(0.5f));
        var buffer = new float[960];
        limiter.Read(buffer, 0, buffer.Length);
        Assert.All(buffer, s => Assert.Equal(0.5f, s));
        Assert.Equal(0.5f, limiter.TakePeak());
        Assert.Equal(0f, limiter.TakePeak()); // taking resets it
    }

    [Fact]
    public void GainRecovers_AfterALoudBurst()
    {
        var source = new Constant(2.0f);
        var limiter = new Limiter(source);
        var buffer = new float[960];
        limiter.Read(buffer, 0, buffer.Length);
        source.Value = 0.5f;
        for (var block = 0; block < 200; block++) limiter.Read(buffer, 0, buffer.Length); // ~2 s
        Assert.Equal(0.5f, buffer[0], 3);
    }

    sealed class Constant(float value) : ISampleProvider
    {
        public float Value { get; set; } = value;
        public WaveFormat WaveFormat => Format;

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Fill(buffer, Value, offset, count);
            return count;
        }
    }
}
