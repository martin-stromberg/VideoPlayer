using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service.Playlists;

namespace VideoPlayer.ViewModels.HomePage
{
    public class BaseHomePageViewModel: BaseViewModel
    {
        
        public BaseHomePageViewModel()
            :base()
        {
            Title = "Videoplayer";
            IsLoading = true;
            Navigate = new Command((args) => { ExecuteNavigate(args.ToString()); });
        }
        #region Loading Status
        public override void ExecuteAppeared()
        {
            base.ExecuteAppeared();
        }
        public override void ExecuteDisappeared()
        {
            base.ExecuteDisappeared();
        }
        protected override void ExecuteFirstAppeared()
        {
            base.ExecuteFirstAppeared();            
            IsLoading = false;
            IsLoaded = true;
        }
        public bool IsLoading { get => GetProperty<bool>(); protected set => SetProperty(value); }
        public bool IsLoaded { get => GetProperty<bool>(); protected set => SetProperty(value); }
        #endregion
        #region Navigation
        public Command Navigate { get; }
        protected virtual void ExecuteNavigate(string navigationCategory)
        {
            
        }
        #endregion
        #region Status
        public string StatusMessage
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
        protected override void OnStatusReceived(string statusMessage)
        {
            base.OnStatusReceived(statusMessage);
            StatusMessage = statusMessage;
        }
        #endregion
        #region Content
        public BaseViewModel NextPlayingContext { get => GetProperty<BaseViewModel>(); set => SetProperty(value); }
        public BaseViewModel NewContext { get => GetProperty<BaseViewModel>(); set => SetProperty(value); }
        #endregion
    }
}
