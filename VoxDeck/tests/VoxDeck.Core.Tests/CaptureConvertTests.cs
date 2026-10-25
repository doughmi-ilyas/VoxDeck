using NAudio.Wave;
using VoxDeck.Core.Capture;

namespace VoxDeck.Core.Tests;

public class CaptureConvertTests
{
    [Fact]
    public void Pcm16Mono_BecomesStereoFloat()
    {
        short[] input = [16384, -32768];
        var bytes = new byte[input.Length * 2];
        Buffer.BlockCopy(input, 0, bytes, 0, bytes.Length);
        var output = new float[4];

        var peak = CaptureConvert.ToStereo(bytes, bytes.Length, new WaveFormat(48000, 16, 1), output, out var frames);

        Assert.Equal(2, frames);
        Assert.Equal([0.5f, 0.5f, -1f, -1f], output);
        Assert.Equal(1f, peak);
    }

    [Fact]
    public void FloatMicArray_ChannelsAreAveraged()
    {
        float[] input = [0.2f, 0.4f, 0.6f, 0.8f]; // one frame from a 4-mic array
        var bytes = new byte[input.Length * 4];
        Buffer.BlockCopy(input, 0, bytes, 0, bytes.Length);
        var output = new float[2];

        CaptureConvert.ToStereo(bytes, bytes.Length, WaveFormat.CreateIeeeFloatWaveFormat(48000, 4), output, out var frames);

        Assert.Equal(1, frames);
        Assert.Equal(0.5f, output[0], 5);
        Assert.Equal(0.5f, output[1], 5);
    }
}
