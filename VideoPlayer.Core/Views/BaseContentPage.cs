using System;
using System.Diagnostics;
using System.Linq;
using VideoPlayer.Service;
using VideoPlayer.Service.Events;
using VideoPlayer.ViewModels;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views
{
    public class BaseContentPage: ContentPage
    {

        private bool _Initialized = false;
        private IApplicationManager _appManager;
        private IEventController _eventController;
        private IViewModelManager _ViewModelManager;
        private bool _AppearedEventSent = false;

        public BaseContentPage()
            : base() { }

        #region Appearing/Disappearing
        protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
        {
            base.OnNavigatingFrom(args);
            (BindingContext as INavigatable)?.ExecuteNavigatingFrom();
        }
        protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
        {
            base.OnNavigatedFrom(args);
            (BindingContext as INavigatable)?.ExecuteNavigatedFrom();
        }
        protected override void OnNavigatedTo(NavigatedToEventArgs args)
        {
            base.OnNavigatedTo(args);
            (BindingContext as INavigatable)?.ExecuteNavigatedTo();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            Initialize();
        }
        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            SendDisappearedEvent();
        }
        private void SendAppearedEvent()
        {
            (BindingContext as IAppearable)?.ExecuteAppeared();
            _AppearedEventSent = true;
        }
        private void SendDisappearedEvent()
        {
            if (_AppearedEventSent)
            {
                (BindingContext as IAppearable)?.ExecuteDisappeared();
                _AppearedEventSent = false;
            }
        }
        #endregion
        #region App Initialization

        private void Initialize()
        {
            if (_Initialized)
            {
                SendAppearedEvent();
                return;
            }
            Initialize(ApplicationManager.GetService<IApplicationManager>());
            _Initialized = true;
        }
        protected bool IsInitialized
        {
            get
            {
                return _appManager is not null && _appManager.Initialized;
            }
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
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    OnLoadingContent(_appManager);
                    SendAppearedEvent();
                }
                catch (Exception ex)
                { 
                    Debug.WriteLine(ex.ToString());
                }
            });
        }

        protected virtual void OnLoadingContent(IApplicationManager applicationManager)
        {
            _eventController = ApplicationManager.GetService<IEventController>();
            _ViewModelManager = ApplicationManager.GetService<IViewModelManager>();
        }
        protected BaseViewModel GetOrCreateViewModel<T>() where T : BaseViewModel
        {
            return _ViewModelManager.Get<T>();
        }
        #endregion
        #region Binding Context
        private object PreviousBindingContext = null;

        protected override void OnBindingContextChanged()
        {
            try
            {
                base.OnBindingContextChanged();
                if (IsInitialized)
                {
                    ReleaseBindingContext(PreviousBindingContext);
                    RegisterBindingContext(BindingContext);
                }
            }
            catch(Exception ex)
            {
                Debug.WriteLine($"{ex}");
            }
        }

        private void RegisterBindingContext(object context)
        {
            if (context is null)
                return;
            PreviousBindingContext = context;
            if (_Initialized)
                SendAppearedEvent();
            OnRegisterBindingContext(context);
        }

        protected virtual void OnRegisterBindingContext(object context)
        {            
                _eventController.Register(context);
        }

        private void ReleaseBindingContext(object context)
        {
            if (context is null)
                return;
            SendDisappearedEvent();
            OnReleaseBindingContect(context);
        }

        protected virtual void OnReleaseBindingContect(object context)
        {
            _eventController.Unregister(context);
        }
        #endregion
        
    }
}
