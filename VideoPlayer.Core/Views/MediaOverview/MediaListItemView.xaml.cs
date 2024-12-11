using VideoPlayer.ViewModels.MediaOverview.MediaItem;

namespace VideoPlayer.Views.MediaOverview;

public partial class MediaListItemView : ContentView
{
	public MediaListItemView()
	{
		InitializeComponent();
	}

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {		
		(BindingContext as BaseListItem)?.Tapped.Execute(bool.Parse((string)e.Parameter));
    }
}