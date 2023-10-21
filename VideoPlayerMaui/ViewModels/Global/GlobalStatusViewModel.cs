using System;
using System.Linq;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.Global
{
    public class GlobalStatusViewModel: BaseViewModel
    {

        public GlobalStatusViewModel(IStatusSubscriber statusSubscriber)
            : base(null)
        {
            statusSubscriber.StatusChanged += (sender, e) => { StatusMessage = e.Message; };
        }

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

    }
}
