using System.ComponentModel;
using System.Diagnostics;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.Video;

public partial class VideoPlayerView : ContentView
{
	public VideoPlayerView()
	{
		InitializeComponent();
	}
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (ViewModel is not null)
            ViewModel.PropertyChanged += OnPropertyChanged;
    }

    protected BaseMediaItemCardViewModel ViewModel
    {
        get => BindingContext as BaseMediaItemCardViewModel;
    }

    private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        switch(e.PropertyName)
        {
            case nameof(BaseMediaItemCardViewModel.PlaybackControlsVisible):
                Video.ShouldShowPlaybackControls = ViewModel.PlaybackControlsVisible;
                break;
        }
    }

    private void Video_PositionChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
    {
        try
        {
            ViewModel?.ExecutePositionChanged(e.Position, Video.Duration);
        }
        catch(Exception ex) 
        {
            Debug.WriteLine(ex);
        }
    }

    private void Video_MediaEnded(object sender, EventArgs e)
    {
        try
        {
            ViewModel?.ExecuteMediaEnded();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void Video_StateChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaStateChangedEventArgs e)
    {
        try
        {
            ViewModel?.ExecuteStateChanged(e.PreviousState, e.NewState);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }

    }
}