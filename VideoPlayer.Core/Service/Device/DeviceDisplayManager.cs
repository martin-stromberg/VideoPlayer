using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using VideoPlayer.Service.BaseServices;
using VideoPlayer.Service.Events;

namespace VideoPlayer.Service.Device
{
    public class DeviceDisplayManager: BaseService, IDeviceDisplayManager
    {

        private int _counter = 0;

        public DeviceDisplayManager(ILogger<DeviceDisplayManager> logger) : base(logger)
        {
        }

        private void Increase()
        {
            if (_counter == 0)
                MainThread.BeginInvokeOnMainThread(() => { DeviceDisplay.KeepScreenOn = true; });
            _counter += 1;
        }

        private void Decrease()
        {
            _counter -= 1;
            if (_counter == 0)
                MainThread.BeginInvokeOnMainThread(() => { DeviceDisplay.KeepScreenOn = false; });
        }

        protected override void ProcessNotification(NotificationEventArgs e)
        {
            base.ProcessNotification(e);
            switch (e.Name)
            {
                case "ProcessStarted":
                    Increase();
                    break;
                case "ProcessFinished":
                    Decrease();
                    break;
            }
        }

        public bool HasRunningProcessed { get => _counter > 0; }
        public async Task<bool> WaitForIdle(TimeSpan timeout)
        {
            DateTime EndTime = DateTime.Now.Add(timeout);
            while (HasRunningProcessed && EndTime > DateTime.Now)
                await Task.Delay(200);
            return !HasRunningProcessed;
        }
    }
}
