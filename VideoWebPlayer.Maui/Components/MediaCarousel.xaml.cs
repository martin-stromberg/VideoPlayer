using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;
using CommunityToolkit.Maui.Core;

namespace VideoWebPlayer.Maui.Components;

public partial class MediaCarousel : ContentView
{
    public MediaCarousel()
    {
        InitializeComponent();
    }

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MediaItemViewModel item)
        {
            if (item.EntryId.HasValue)
            {
                Page? detailPage = null;
                
                // Navigiere basierend auf dem MediaType
                if (item.MediaType == "show")
                {
                    detailPage = new TVShowDetailsPage(item.EntryId.Value);
                }
                else if (item.MediaType == "collection")
                {
                    detailPage = new MovieCollectionDetailsPage(item.EntryId.Value);
                }

                if (detailPage != null && Application.Current?.Windows.Count > 0)
                {
                    await Application.Current.Windows[0].Page?.Navigation.PushAsync(detailPage);
                }
            }
        }
    }

    private void TouchBehavior_LongPressCompleted(object sender, LongPressCompletedEventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MediaItemViewModel item)
        {
            item.DeleteDownloadCommand?.Execute(e.LongPressCommandParameter);
        }
    }
}
