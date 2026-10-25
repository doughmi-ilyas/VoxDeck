using NAudio.CoreAudioApi;

namespace VoxDeck.Core.Devices;

public sealed record AudioDevice(string Id, string Name, bool IsDefault);

// Read-only: lists what Windows already exposes, never changes device settings.
public static class AudioDevices
{
    public static List<AudioDevice> Microphones() => List(DataFlow.Capture);

    public static AudioDevice? DefaultOutput() => List(DataFlow.Render).FirstOrDefault(d => d.IsDefault);

    static List<AudioDevice> List(DataFlow flow)
    {
        using var devices = new MMDeviceEnumerator();
        var defaultId = devices.HasDefaultAudioEndpoint(flow, Role.Console)
            ? devices.GetDefaultAudioEndpoint(flow, Role.Console).ID
            : null;
        return devices.EnumerateAudioEndPoints(flow, DeviceState.Active)
            .Select(d => new AudioDevice(d.ID, d.FriendlyName, d.ID == defaultId))
            .ToList();
    }
}
