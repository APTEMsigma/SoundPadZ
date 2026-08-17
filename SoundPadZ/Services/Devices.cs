using NAudio.CoreAudioApi;

namespace SoundPadZ.Services;

public sealed class DevInfo
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public static class Devices
{
    public static List<DevInfo> List(DataFlow flow)
    {
        var result = new List<DevInfo>();
        try
        {
            foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                string name;
                try
                {
                    name = device.FriendlyName;
                }
                catch
                {
                    name = flow == DataFlow.Render ? "Device" : "Microphone";
                }
                result.Add(new DevInfo { Id = device.ID, Name = name });
            }
        }
        catch
        {
            // audio service unavailable: return what we have (possibly nothing)
        }
        return result;
    }
}
