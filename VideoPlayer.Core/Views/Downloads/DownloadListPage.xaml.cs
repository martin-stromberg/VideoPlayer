using VideoPlayer.Service;
using VideoPlayer.ViewModels.Downloads;

namespace VideoPlayer.Views.Downloads;

public partial class DownloadListPage : BaseContentPage
{
	public DownloadListPage()
	{
		InitializeComponent();
	}
    protected override void OnLoadingContent(IApplicationManager applicationManager)
    {
        base.OnLoadingContent(applicationManager);
        BindingContext = applicationManager.ResolveService<DownloadListViewModel>();
    }
}