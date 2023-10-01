using CommunityToolkit.Maui.Views;
using VideoMeister.ViewModels;

namespace VideoMeister;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

/* Nicht gemergte Änderung aus Projekt "VideoMeister (net7.0-ios)"
Vor:
        BindingContext = ViewModel = new MainPageViewModel(App.GetService<Services.VideoSourceManager>());
Nach:
        BindingContext = ViewModel = new MainPageViewModel(App.GetService<VideoSourceManager>());
*/
        BindingContext = ViewModel = new MainPageViewModel(App.GetService<Services.VideoSources.VideoSourceManager>());
    }

    public MainPageViewModel ViewModel { get; }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.OnPageAppearing();
    }

    private void ContentPage_Unloaded(object sender, EventArgs e)
    {
        mediaElement.Handler?.DisconnectHandler();
        //mediaElement.CurrentState == CommunityToolkit.Maui.Core.Primitives.MediaElementState.Playing
    }

    private void mediaElement_MediaEnded(object sender, EventArgs e)
    {
        ViewModel.ProcessMediaEnded();
    }

    private void mediaElement_MediaFailed(object sender, CommunityToolkit.Maui.Core.Primitives.MediaFailedEventArgs e)
    {
        ViewModel.ProcessMediaFailed(e.ErrorMessage);
    }

    private void mediaElement_MediaOpened(object sender, EventArgs e)
    {
        ViewModel.ProcessMediaOpened();
    }

    private void mediaElement_PositionChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
    {
        ViewModel.ProcessMediaPositionChanged(e.Position);
    }

    private void mediaElement_SeekCompleted(object sender, EventArgs e)
    {
        ViewModel.ProcessMediaSeekCompleted();
    }

    private void OnPlayPauseButtonClicked(object sender, EventArgs e)
    {

    }

    private void OnStopButtonClicked(object sender, EventArgs e)
    {

    }
}

