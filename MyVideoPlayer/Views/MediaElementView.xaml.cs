using MyVideoPlayer.ViewModels;

namespace MyVideoPlayer.Views;

public partial class MediaElementView : ContentView
{
	public MediaElementView()
	{
		InitializeComponent();
	}

    protected IMediaElementViewModel ViewModel
    {
        get { return BindingContext as IMediaElementViewModel; }
    }

    private void mediaElement_MediaEnded(object sender, EventArgs e)
    {
        ViewModel.MediaEnded();
    }

    private void mediaElement_MediaFailed(object sender, CommunityToolkit.Maui.Core.Primitives.MediaFailedEventArgs e)
    {
        ViewModel.MediaFailed(e.ErrorMessage);
    }

    private void mediaElement_MediaOpened(object sender, EventArgs e)
    {
        ViewModel.MediaOpened();
    }

    private void mediaElement_PositionChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
    {
        //ViewModel.PositionChanged(e.Position);
    }

    private void mediaElement_SeekCompleted(object sender, EventArgs e)
    {
        ViewModel.SeekCompleted();
    }

    private void ContentView_Unloaded(object sender, EventArgs e)
    {
        //mediaElement.Handler?.DisconnectHandler();
    }
}