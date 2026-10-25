using NAudio.Wave;
using VoxDeck.Core.Audio;

namespace VoxDeck.Core.Tests;

public class VoiceTests
{
    // 10 Hz stereo → 20 samples per second; sample value == its index.
    static readonly WaveFormat Format = WaveFormat.CreateIeeeFloatWaveFormat(10, 2);
    static readonly float[] TwoSeconds = Enumerable.Range(0, 40).Select(i => (float)i).ToArray();

    [Fact]
    public void Seek_StartsReadingFromThatTime()
    {
        var playback = new Voice(null, 0, Format, TwoSeconds) { Position = TimeSpan.FromSeconds(0.5) };
        var buffer = new float[4];
        playback.Read(buffer, 0, 4);
        Assert.Equal([10f, 11, 12, 13], buffer);
        Assert.Equal(TimeSpan.FromSeconds(0.7), playback.Position);
    }

    [Fact]
    public void Loop_WrapsToLoopStart_WithoutEnding()
    {
        var playback = new Voice(null, 0, Format, TwoSeconds)
        {
            Loop = true,
            LoopStart = TimeSpan.FromSeconds(1),
            Position = TimeSpan.FromSeconds(1.5),
        };
        var buffer = new float[14];
        Assert.Equal(14, playback.Read(buffer, 0, 14));
        Assert.Equal([30f, 31, 32, 33, 34, 35, 36, 37, 38, 39, 20, 21, 22, 23], buffer);
        Assert.False(playback.Ended);
    }

    [Fact]
    public void NoLoop_EndsAtTheEnd()
    {
        var playback = new Voice(null, 0, Format, TwoSeconds) { Position = TimeSpan.FromSeconds(1.9), Volume = 0.5f };
        var buffer = new float[4];
        Assert.Equal(2, playback.Read(buffer, 0, 4));
        Assert.Equal([19f, 19.5f, 0, 0], buffer);
        Assert.True(playback.Ended);
    }
}
