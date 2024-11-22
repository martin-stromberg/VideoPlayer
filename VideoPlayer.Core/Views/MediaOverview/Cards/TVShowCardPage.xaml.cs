using Microsoft.Maui.Controls;
using System;
using VideoPlayer.Service;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.MediaOverview.Cards;

[QueryProperty(nameof(ElementId), "Id")]
public partial class TVShowCardPage : BaseCardPage
{
	public TVShowCardPage()
		:base()
	{
		InitializeComponent();
	}

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
                BindingContext = CreateCardViewModel<TVShowCardViewModel, TVShow>(Show);
                break;
            case EntryType.TVShowEpisode:
                BindingContext = CreateCardViewModel<TVShowCardViewModel, TVShowEpisode>(Episode);
                break;
        }
    }
}