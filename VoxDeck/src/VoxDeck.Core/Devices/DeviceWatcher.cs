using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace VoxDeck.Core.Devices;

// Windows tells us when audio devices appear, disappear or the default changes — no polling.
// Callbacks arrive on a COM thread and must not call back into the audio APIs, so Changed only signals.
public sealed class DeviceWatcher : IMMNotificationClient, IDisposable
{
    readonly MMDeviceEnumerator _devices = new();

    public DeviceWatcher() => _devices.RegisterEndpointNotificationCallback(this);

    public event Action? Changed;

    public void OnDeviceStateChanged(string deviceId, DeviceState newState) => Changed?.Invoke();
    public void OnDeviceAdded(string deviceId) => Changed?.Invoke();
    public void OnDeviceRemoved(string deviceId) => Changed?.Invoke();

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
    {
        if (role == Role.Console) Changed?.Invoke();
    }

    public void OnPropertyValueChanged(string deviceId, PropertyKey key) { } // fires constantly; nothing we need

    public void Dispose()
    {
        _devices.UnregisterEndpointNotificationCallback(this);
        _devices.Dispose();
    }
}
