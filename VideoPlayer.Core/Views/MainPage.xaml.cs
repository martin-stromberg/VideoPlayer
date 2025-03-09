
using VideoPlayer.Service;
using VideoPlayer.Service.ErrorHandling;
using VideoPlayer.ViewModels.HomePage;
using static System.Net.Mime.MediaTypeNames;

namespace VideoPlayer.Views
{
    public partial class MainPage: BaseContentPage
    {
        private IApplicationManager applicationManager;
        private IEmail _emailService;
        private bool _IsSendingMail = false;
        public MainPage()
        {
            InitializeComponent();
            BindingContext = new BaseHomePageViewModel(null, null, null);
        }

        protected override void OnLoadingContent(IApplicationManager applicationManager)
        {
            base.OnLoadingContent(applicationManager);
            this.applicationManager = applicationManager;
            _emailService = Email.Default;
            BtnSendErrorMail.IsEnabled = _emailService is not null;
            var errorManager = applicationManager.ResolveService<IErrorLogManager>();
            if (errorManager.HasErrors)
                BindingContext = applicationManager.ResolveService<ErrorViewModel>();
            else
            BindingContext = applicationManager.ResolveService<HomePageViewModel>();
        }

        private void Button_Clicked(object sender, EventArgs e)
        {
            switch((sender as Button).CommandParameter)
            {
                case "share":
                    SendMail($"Fehlerbericht", (BindingContext as ErrorViewModel).ErrorMessages);                    
                    break;
            }
            BindingContext = applicationManager.ResolveService<HomePageViewModel>();
        }

        private async void SendMail(string subject, string message)
        {
            if (!_IsSendingMail)
                try
                {
                    _IsSendingMail = true;

                    if (_emailService.IsComposeSupported)
                    {
                        var emailMessage = new EmailMessage
                        {
                            Subject = subject,
                            Body = message,
                            To = new List<string> { "mstromberg84+videoplayer@icloud.com" }
                        };

                        await _emailService.ComposeAsync(emailMessage);
                    }
                    else
                    {
                        await DisplayAlert("Unsupported",
                           "The opening of the email client is currently not supported.", "OK");
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", ex.ToString(), "OK");
                }
                finally
                {
                    _IsSendingMail = false;
                }
        }
    }
}