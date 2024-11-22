using VideoPlayer.ViewModels.Downloads;

namespace VideoPlayer.Views.Downloads;

public partial class MediaListItemViewPanel : ContentView
{
	public MediaListItemViewPanel()
	{
		InitializeComponent();
	}

    private void ImageButton_Clicked(object sender, EventArgs e)
    {
		(BindingContext as IDownloadListItem)?.ExecuteDelete();
    }
}