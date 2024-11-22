using System;
using VideoPlayer.Service;
using VideoPlayer.Service.Download;
using VideoPlayer.Service.Library;
using VideoPlayer.Service.Library.Models.Classified;
using VideoPlayer.Service.Playlists;
using VideoPlayer.Service.Resources;
using VideoPlayer.ViewModels.MediaOverview.Cards;

namespace VideoPlayer.Views.MediaOverview.Cards;

[QueryProperty(nameof(ElementId), "Id")]
[QueryProperty(nameof(AutoPlay), "AutoPlay")]
public partial class MovieCardPage : BaseCardPage
{
    

    public MovieCardPage()
		:base()
	{
		InitializeComponent();
    }
    public override bool AutoPlay { get => base.AutoPlay; set => base.AutoPlay = value; }
    public override long ElementId { get => base.ElementId; set => base.ElementId = value; }
    protected Movie Movie { get => base.Entry as Movie; }
    protected MovieCollection Collection { get => base.Entry as MovieCollection; }
    protected override void OnLoadingContent(IApplicationManager applicationManager)
    {
        base.OnLoadingContent(applicationManager);
        if (Entry is not null)
        switch (Entry.Type)
        {
            case EntryType.Movie:
                BindingContext = CreateCardViewModel<MovieCardViewModel, Movie>(Movie, AutoPlay);
                break;
            case EntryType.MovieCollection:
                BindingContext = CreateCardViewModel<MovieCardViewModel, MovieCollection>(Collection, AutoPlay);
                break;
        }
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        (BindingContext as BaseMediaItemCardViewModel)?.PlaybackCommand.Execute(null);
    }
}