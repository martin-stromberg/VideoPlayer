using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VideoPlayer.Service;
using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.Views.MediaOverview
{
    public class TVShowOverviewPage: MediaOverviewPage
    {
        public TVShowOverviewPage()
            : base()
        {
        }
        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            BindingContext = GetOrCreateViewModel<TVShowOverviewViewModel>();
        }
    }
}
