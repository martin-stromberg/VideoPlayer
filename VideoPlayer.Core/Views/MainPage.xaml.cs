
using VideoPlayer.Service;
using VideoPlayer.ViewModels.HomePage;

namespace VideoPlayer.Views
{
    public partial class MainPage: BaseContentPage
    {

        public MainPage()
        {
            InitializeComponent();
            BindingContext = new BaseHomePageViewModel();
        }

        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            BindingContext = applicationManager.ResolveService<HomePageViewModel>();
        }
    }
}