using VoxDeck.Core.Devices;

namespace VoxDeck.Core.Tests;

public class VirtualCablesTests
{
    static AudioDevice D(string name) => new(name, name, false);

    // Device names as Windows shows them on this machine plus the common cable drivers.
    static readonly AudioDevice[] Outputs =
    [
        D("Casque (2- Realtek(R) Audio)"),
        D("Virtual Speakers (Virtual Speakers for AudioRelay)"),
        D("CABLE Input (VB-Audio Virtual Cable)"),
        D("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)"),
        D("Line 1 (Virtual Audio Cable)"),
    ];

    static readonly AudioDevice[] Mics =
    [
        D("Réseau de microphones (2- Technologie Intel® Smart Sound pour microphones numériques)"),
        D("Virtual Mic (Virtual Mic for AudioRelay)"),
        D("CABLE Output (VB-Audio Virtual Cable)"),
        D("VoiceMeeter Output (VB-Audio VoiceMeeter VAIO)"),
        D("Line 1 (Virtual Audio Cable)"),
    ];

    [Fact]
    public void PairsEachCableWithItsMicrophoneSide()
    {
        var cables = VirtualCables.Find(Outputs, Mics);
        Assert.Equal(
            [
                ("CABLE Input (VB-Audio Virtual Cable)", "CABLE Output (VB-Audio Virtual Cable)"),
                ("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)", "VoiceMeeter Output (VB-Audio VoiceMeeter VAIO)"),
                ("Line 1 (Virtual Audio Cable)", "Line 1 (Virtual Audio Cable)"),
            ],
            cables.Select(c => (c.Speaker.Name, c.Microphone.Name)));
    }

    [Fact]
    public void IgnoresRealDevicesAndUnpairedVirtualOnes()
    {
        // AudioRelay's speakers and mic are separate devices, not a loopback pair.
        Assert.Empty(VirtualCables.Find(Outputs[..2], Mics[..2]));
    }

    [Fact]
    public void CableWithoutItsMicrophoneSide_IsNotOffered() =>
        Assert.Empty(VirtualCables.Find([D("CABLE Input (VB-Audio Virtual Cable)")], Mics[..2]));
}
