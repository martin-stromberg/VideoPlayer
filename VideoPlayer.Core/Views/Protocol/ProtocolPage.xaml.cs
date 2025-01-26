using Microsoft.Maui.Controls;
using VideoPlayer.Service;
using VideoPlayer.ViewModels.Protocol;

namespace VideoPlayer.Views.Protocol;

[QueryProperty(nameof(ElementId), "Id")]
[QueryProperty(nameof(ElementType), "Type")]
public partial class ProtocolPage : BaseContentPage
{
    private ProtocolViewModel viewModel;

    public ProtocolPage()
	{
		InitializeComponent();
	}
    public long ElementId { get; set; }
    public string ElementType { get; set; }
    protected override void OnLoadingContent(IApplicationManager applicationManager)
    {
        base.OnLoadingContent(applicationManager);
        BindingContext = viewModel = applicationManager.ResolveService<ProtocolViewModel>();
        viewModel.LoadParent(ElementType, ElementId);
    }
}