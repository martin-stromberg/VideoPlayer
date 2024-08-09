using System;
using System.Linq;
using VideoPlayer.Service;

namespace VideoPlayer.Views
{
    public class BaseContentPage: ContentPage
    {

        private bool _Initialized = false;
        private IApplicationManager _appManager;

        public BaseContentPage()
            : base() { }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Initialize();
        }

        #region App Initialization
        private void Initialize()
        {
            if (_Initialized)
                return;
            Initialize(ApplicationManager.GetService<IApplicationManager>());
            _Initialized = true;
        }

        private void Initialize(IApplicationManager appManager)
        {
            _appManager = appManager;
            if (_appManager.Initialized)
                AppInitializer_Initialized(this, EventArgs.Empty);
            else
            {
                _appManager.InitializationCompleted += AppInitializer_Initialized;
                _appManager.Initialize();
            }
        }

        private void AppInitializer_Initialized(object sender, EventArgs e)
        {
            OnLoadingContent(_appManager);
        }

        protected virtual void OnLoadingContent(IApplicationManager applicationManager) { }
        #endregion

    }
}
