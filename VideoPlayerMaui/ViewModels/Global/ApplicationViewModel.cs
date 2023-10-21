using System;
using System.Linq;
using VideoPlayer.StatusManagement;
using VideoPlayer.ViewModels.Homepage;

namespace VideoPlayer.ViewModels.Global
{
    public class ApplicationViewModel: BaseViewModel
    {

        public ApplicationViewModel(IServiceProvider serviceProvider, IStatusPublisher statusPublisher)
            : base(statusPublisher)
        {
            Title = "Medienbibliothek";
            StatusViewModel = serviceProvider.GetService<GlobalStatusViewModel>();
            ContentViewModel = serviceProvider.GetService<HomePageViewModel>();
        }

        public override void OnAppeared()
        {
            base.OnAppeared();
            LoadContent();
        }

        #region Startup initialization
        public bool IsInitialized
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

        public bool IsInitializing
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

        private async void LoadContent()
        {
            if (IsInitialized)
                return;
            IsInitializing = true;
            try
            {
                AddStatusMessage("Initialisiere");
                await Task.Delay(2000);
                AddStatusMessage(string.Empty);
            }
            finally
            {
                IsInitializing = false;
            }
            IsInitialized = true;
        }
        #endregion

        public HomePageViewModel ContentViewModel
        {
            get
            {
                return GetProperty<HomePageViewModel>();
            }
            set
            {
                SetProperty<HomePageViewModel>(value);
            }
        }

        public GlobalStatusViewModel StatusViewModel
        {
            get
            {
                return GetProperty<GlobalStatusViewModel>();
            }
            set
            {
                SetProperty<GlobalStatusViewModel>(value);
            }
        }

    }
}
