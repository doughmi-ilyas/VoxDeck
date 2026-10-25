namespace VoxDeck.Core.Devices;

/// <param name="Speaker">The playback side VoxDeck writes the mix to, e.g. "CABLE Input".</param>
/// <param name="Microphone">The recording side voice apps pick as their microphone, e.g. "CABLE Output".</param>
public sealed record VirtualCable(AudioDevice Speaker, AudioDevice Microphone);

// Virtual audio cables are driver pairs: sound played into one side comes out of the other as a microphone.
// Windows names both sides after the same driver, e.g. "CABLE Input (VB-Audio Virtual Cable)" and
// "CABLE Output (VB-Audio Virtual Cable)", which is how the pairs are matched.
public static class VirtualCables
{
    static readonly string[] Known = ["VB-Audio", "CABLE", "VoiceMeeter", "Virtual Audio Cable"];

    public static bool LooksVirtual(string name) => Known.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));

    public static List<VirtualCable> Find(IEnumerable<AudioDevice> outputs, IEnumerable<AudioDevice> microphones)
    {
        var mics = microphones.ToList();
        return outputs
            .Where(o => LooksVirtual(o.Name))
            .Select(o => (Speaker: o, Microphone: mics.FirstOrDefault(m => LooksVirtual(m.Name) && DriverName(m.Name) == DriverName(o.Name))))
            .Where(pair => pair.Microphone is not null)
            .Select(pair => new VirtualCable(pair.Speaker, pair.Microphone!))
            .ToList();
    }

    // "CABLE Input (VB-Audio Virtual Cable)" → "VB-Audio Virtual Cable"
    static string DriverName(string name)
    {
        var open = name.LastIndexOf('(');
        return open < 0 ? name : name[(open + 1)..].TrimEnd(')', ' ');
    }
}
