using Microsoft.Maui.Controls;
using System;
using VideoPlayer.Service;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.MediaOverview.Cards;

[QueryProperty(nameof(ElementId), "Id")]
[QueryProperty(nameof(AutoPlay), "AutoPlay")]

public partial class TVShowCardPage : BaseCardPage
{
	public TVShowCardPage()
		:base()
	{
		InitializeComponent();
	}
    public override bool AutoPlay { get => base.AutoPlay; set => base.AutoPlay = value; }
    public override long ElementId { get => base.ElementId; set => base.ElementId = value; }
    protected TVShow Show { get => base.Entry as TVShow; }
    protected TVShowSeason Season { get => base.Entry as TVShowSeason; }
    protected TVShowEpisode Episode { get => base.Entry as TVShowEpisode; }
    protected override void OnLoadingContent(IApplicationManager applicationManager)
    {
        base.OnLoadingContent(applicationManager);        
        switch (Entry.Type)
        {
            case EntryType.TVShow:
                BindingContext = CreateCardViewModel<TVShowCardViewModel, TVShow>(Show, AutoPlay);
                break;
            case EntryType.TVShowEpisode:
                BindingContext = CreateCardViewModel<TVShowCardViewModel, TVShowEpisode>(Episode, AutoPlay);
                break;
        }
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        (BindingContext as BaseMediaItemCardViewModel)?.PlaybackCommand.Execute(null);
    }
}