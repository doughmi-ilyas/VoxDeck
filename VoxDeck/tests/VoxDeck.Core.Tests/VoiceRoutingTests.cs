using VoxDeck.Core.Devices;

namespace VoxDeck.Core.Tests;

public class VoiceRoutingTests
{
    static readonly AudioDevice RealMic = new("mic", "Microphone (Realtek)", IsDefault: false);
    static readonly AudioDevice ArrayMic = new("array", "Microphone Array (Intel Smart Sound)", IsDefault: false);
    static readonly AudioDevice CableMic = new("cable-out", "CABLE Output (VB-Audio Virtual Cable)", IsDefault: true);
    static readonly VirtualCable Cable = new(new("cable-in", "CABLE Input (VB-Audio Virtual Cable)", false), CableMic);

    [Fact]
    public void NeverPicksTheCableMicrophone_EvenWhenItIsTheWindowsDefault()
    {
        var route = VoiceRouting.Choose(true, [Cable], [CableMic, RealMic], null, null);
        Assert.Equal(VoiceState.Running, route.State);
        Assert.Equal(RealMic, route.Microphone);
    }

    [Fact]
    public void SavedChoicesWin_WhenStillPluggedIn()
    {
        var route = VoiceRouting.Choose(true, [Cable], [RealMic, ArrayMic], "cable-in", "array");
        Assert.Equal((VoiceState.Running, Cable, ArrayMic), (route.State, route.Cable, route.Microphone));
    }

    [Fact]
    public void UnpluggedSavedMic_FallsBackToAnotherRealMic() =>
        Assert.Equal(RealMic, VoiceRouting.Choose(true, [Cable], [RealMic], "cable-in", "gone").Microphone);

    [Fact]
    public void SavedCableMicAsVoice_IsReportedInsteadOfLooping() =>
        Assert.Equal(VoiceState.CableAsMic, VoiceRouting.Choose(true, [Cable], [CableMic, RealMic], null, "cable-out").State);

    [Theory]
    [InlineData(false, true, true, VoiceState.Off)]
    [InlineData(true, false, true, VoiceState.NoCable)]
    [InlineData(true, true, false, VoiceState.NoMic)]
    public void ExplainsWhyItIsNotRunning(bool enabled, bool hasCable, bool hasMic, VoiceState expected)
    {
        VirtualCable[] cables = hasCable ? [Cable] : [];
        AudioDevice[] mics = hasMic ? [RealMic] : [];
        Assert.Equal(expected, VoiceRouting.Choose(enabled, cables, mics, null, null).State);
    }
}
