
using VideoPlayer.Service;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.ViewModels.HomePage;

namespace VideoPlayer.Views
{
    public partial class MainPage: BaseContentPage
    {
        private IApplicationManager applicationManager;
        public MainPage()
        {
            InitializeComponent();
            BindingContext = new BaseHomePageViewModel();
        }

        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            this.applicationManager = applicationManager;
            var errorManager = applicationManager.ResolveService<IErrorLogManager>();
            if (errorManager.HasErrors)
                BindingContext = applicationManager.ResolveService<ErrorViewModel>();
            else
            BindingContext = applicationManager.ResolveService<HomePageViewModel>();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            BindingContext = applicationManager.ResolveService<HomePageViewModel>();
        }
    }
}