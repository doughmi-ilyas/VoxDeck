using VoxDeck.Core.Audio;

namespace VoxDeck.Core.Tests;

public class SoundRouteTests
{
    const string Headset = "headset";
    const string Cable = "cable";

    [Theory]
    [InlineData(SoundRoute.Both, new[] { Headset, Cable })]
    [InlineData(SoundRoute.MeOnly, new[] { Headset })]
    [InlineData(SoundRoute.CallOnly, new[] { Cable })]
    public void PicksTheRightOutputs_WhileMixing(SoundRoute route, string[] expected) =>
        Assert.Equal(expected, SoundRoutes.Pick(route, Headset, Cable));

    [Fact]
    public void Both_StillPlaysForYou_WhenMixingIsOff() =>
        Assert.Equal([Headset], SoundRoutes.Pick(SoundRoute.Both, Headset, null));

    [Fact]
    public void CallOnly_HasNowhereToPlay_WhenMixingIsOff() =>
        Assert.Empty(SoundRoutes.Pick(SoundRoute.CallOnly, Headset, null));

    [Fact]
    public void OldLibraries_DefaultToBoth() =>
        Assert.Equal(SoundRoute.Both, new Library.Sound("a", "b").Route);
}
