using VideoWebPlayer.Maui.ViewModels;
using VideoWebPlayer.Maui.Services;
using VideoWebPlayer.Maui.Models;
using CommunityToolkit.Maui.Core;

namespace VideoWebPlayer.Maui.Components;

public partial class MediaCarousel : ContentView
{
    // Default item size (can be adjusted based on device)
    public double ItemWidth { get; set; } = 140;
    public double ItemHeight { get; set; } = 210;

    public MediaCarousel()
    {
        InitializeComponent();

        // Adjust default sizes for smaller devices
        try
        {
            if (DeviceInfo.Platform == DevicePlatform.iOS || DeviceInfo.Platform == DevicePlatform.Android)
            {
                var width = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
                // On small screens reduce item size
                if (width <= 768) // e.g. iPad mini width in portrait may be <= 768
                {
                    ItemWidth = 120;
                    ItemHeight = 180;
                }
                else
                {
                    ItemWidth = 160;
                    ItemHeight = 240;
                }
            }
        }
        catch { }
    }

    private async void OnItemTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable && bindable.BindingContext is MediaItemViewModel item)
        {
            if (item.EntryId.HasValue)
            {
                Page? detailPage = null;

                // Navigiere basierend auf dem MediaType
                // Falls MediaType null ist (z.B. bei Favoriten mit Seasons/Episodes), 
                // versuche zur Show zu navigieren
                if (item.MediaType == "show" || string.IsNullOrEmpty(item.MediaType))
                {
                    detailPage = new TVShowDetailsPage(item.EntryId.Value, item.SeasonId, item.EpisodeId);
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
            // Delegate handling to the carousel's viewmodel so it can decide what to do
            if (BindingContext is MediaCarouselViewModel vm)
            {
                vm.ExecuteLongPress(item);
            }
            else
            {
                // fallback: execute download delete command if present
                item.DeleteDownloadCommand?.Execute(e.LongPressCommandParameter);
            }
        }
    }
}
