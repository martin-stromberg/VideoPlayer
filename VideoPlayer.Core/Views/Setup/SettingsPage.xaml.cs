
using VideoPlayer.Service;
using VideoPlayer.ViewModels.Setup;

namespace VideoPlayer.Views.Setup
{
    public partial class SettingsPage: BaseContentPage
    {

        public SettingsPage()
        {
            InitializeComponent();
        }

        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            BindingContext = applicationManager.ResolveService<SettingsViewModel>();
        }

    }
}