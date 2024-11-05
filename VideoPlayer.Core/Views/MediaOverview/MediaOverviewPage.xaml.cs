using VideoPlayer.ViewModels.MediaOverview;

namespace VideoPlayer.Views.MediaOverview;

public partial class MediaOverviewPage : BaseContentPage
{
	public MediaOverviewPage()
	{
		InitializeComponent();
	}

    private void ScrollView_Scrolled(object sender, ScrolledEventArgs e)
    {
        if (!(sender is ScrollView scrollView)) return;

        var scrollSpace = scrollView.ContentSize.Height - scrollView.Height;

        if (scrollSpace-500 > e.ScrollY) return;

        (BindingContext as BaseMediaOverviewViewModel).LoadNextItems();
    }
}