using System.Diagnostics.Metrics;

namespace VideoPlayer.Service.Device
{
    public interface IDeviceDisplayManager {
        bool HasRunningProcessed { get; }
        Task<bool> WaitForIdle(TimeSpan timeout);
    }
}