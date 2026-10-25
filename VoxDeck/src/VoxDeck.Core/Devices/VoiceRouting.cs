namespace VoxDeck.Core.Devices;

public enum VoiceState { Off, NoCable, NoMic, CableAsMic, Running, Failed }

public sealed record VoiceRoute(VoiceState State, VirtualCable? Cable, AudioDevice? Microphone);

public static class VoiceRouting
{
    /// <summary>
    /// Picks the cable and microphone for voice mixing. The saved choices win when still plugged in;
    /// otherwise the first cable and the Windows default mic. A cable's own microphone side is never picked
    /// automatically, because feeding the mix back into itself would echo forever.
    /// </summary>
    public static VoiceRoute Choose(bool enabled, IReadOnlyList<VirtualCable> cables, IReadOnlyList<AudioDevice> microphones,
        string? savedCableId, string? savedMicId)
    {
        bool IsCableMic(AudioDevice m) => cables.Any(c => c.Microphone.Id == m.Id);
        var cable = cables.FirstOrDefault(c => c.Speaker.Id == savedCableId) ?? cables.FirstOrDefault();
        var mic = microphones.FirstOrDefault(m => m.Id == savedMicId)
                  ?? microphones.FirstOrDefault(m => m.IsDefault && !IsCableMic(m))
                  ?? microphones.FirstOrDefault(m => !IsCableMic(m));
        var state = !enabled ? VoiceState.Off
            : cable is null ? VoiceState.NoCable
            : mic is null ? VoiceState.NoMic
            : IsCableMic(mic) ? VoiceState.CableAsMic
            : VoiceState.Running;
        return new VoiceRoute(state, cable, mic);
    }
}
