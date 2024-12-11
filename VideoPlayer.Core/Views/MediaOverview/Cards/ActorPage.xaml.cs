using VideoPlayer.Service;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.MediaOverview.Cards;

[QueryProperty(nameof(ElementId), "Id")]
[QueryProperty(nameof(AutoPlay), "AutoPlay")]
public partial class ActorPage : BaseCardPage
{
	public ActorPage()
	{
		InitializeComponent();
	}
    public override bool AutoPlay { get => base.AutoPlay; set => base.AutoPlay = value; }
    public override long ElementId { get => base.ElementId; set => base.ElementId = value; }
    public Actor Actor { get; private set; }
    protected override void OnLoadingContent(IApplicationManager applicationManager)
    {
        base.OnLoadingContent(applicationManager);
        Actor = MediaLibrary.GetActor(ElementId);
        BindingContext = CreateCardViewModel<ActorCardViewModel, Actor>(Actor, AutoPlay);
    }
}