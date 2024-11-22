using VideoPlayer.ViewModels;

namespace VideoPlayer.Tests.Views
{
    public class TestViewModel: BaseViewModel
    {

        private TestManager _TestManager;

        public TestViewModel()
        {
            _TestManager = new TestManager();
            _TestManager.Finished += (sender, e) =>
            {
                Enabled = true;
                Message = e.Message;
            };
            Run = new Command(() => { ExecuteRun(); });
            DeviceDisplay.KeepScreenOn = true;
            Enabled = true;
        }

        private void ExecuteRun()
        {
            Enabled = false;
            Message = string.Empty;
            _TestManager.Start(ForceAll);
        }

        public Command Run { get; }

        public bool Enabled
        {
            get
            {
                return GetProperty<bool>();
            }
            set
            {
                SetProperty<bool>(value);
            }
        }

        public string Message
        {
            get
            {
                return GetProperty<string>();
            }
            set
            {
                SetProperty<string>(value);
            }
        }

        public bool ForceAll { get; internal set; }
    }
}
