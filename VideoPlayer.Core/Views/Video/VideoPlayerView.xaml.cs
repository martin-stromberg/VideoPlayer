using System.ComponentModel;
using System.Diagnostics;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.Video;

public partial class VideoPlayerView : ContentView
{
    private TimeSpan positionToSeek;

    public VideoPlayerView()
	{
		InitializeComponent();
	}
    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += OnPropertyChanged;
            ViewModel.SeekRequested += ViewModel_SeekRequested;
        }
    }

    private void ViewModel_SeekRequested(object sender, TimeSpanEventArgs e)
    {
        try
        {
            switch (Video.CurrentState)
            {
                case CommunityToolkit.Maui.Core.Primitives.MediaElementState.Buffering:
                case CommunityToolkit.Maui.Core.Primitives.MediaElementState.Failed:
                case CommunityToolkit.Maui.Core.Primitives.MediaElementState.Paused:
                case CommunityToolkit.Maui.Core.Primitives.MediaElementState.Stopped:
                case CommunityToolkit.Maui.Core.Primitives.MediaElementState.Opening:
                    return;
            }
            Video.SeekTo(e.Position);
        }
        finally
        {
            positionToSeek = e.Position;
        }
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
    private double lastVolume = 0;
    private void CheckVolumeChanged()
    {
        if (lastVolume == Video.Volume)
            return;
        lastVolume = Video.Volume;
        ;// ViewModel?.ExecuteVolumeChanged(lastVolume);
    }
    private void Video_PositionChanged(object sender, CommunityToolkit.Maui.Core.Primitives.MediaPositionChangedEventArgs e)
    {
        try
        {
            if (positionToSeek != TimeSpan.Zero)
            {
                var newPosition = positionToSeek;
                positionToSeek = TimeSpan.Zero;
                Video.SeekTo(newPosition);
            }
            else
            {
                ViewModel?.ExecutePositionChanged(e.Position, Video.Duration);
                CheckVolumeChanged();
            }
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

    private void Video_MediaOpened(object sender, EventArgs e)
    {
        try
        {
            ViewModel?.ExecuteMediaOpened();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}