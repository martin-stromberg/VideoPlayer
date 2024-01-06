using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Navigation;
using VideoPlayer.Services.Settings;
using VideoPlayer.StatusManagement;

namespace VideoPlayer.ViewModels.MediaLists.Details
{
    public class MovieDetailsViewModel : BaseViewModel
    {
        public MovieDetailsViewModel(
            IStatusPublisher statusPublisher, 
            INavigationManager navigationManager, 
            ISettingsService settings) 
            : base(statusPublisher, navigationManager, settings)
        {
        }
    }
}
