using Microsoft.Maui.Controls;
using System.Diagnostics;
using VideoPlayer.Service.Library.Models;
using VideoPlayer.ViewModels.Common;
using VideoPlayer.ViewModels.MediaOverview.MediaItem;
using VideoPlayer.Views.MediaOverview;

namespace VideoPlayer.Views.Common;

public partial class HorizontalMediaCollectionView : ContentView
{
	public HorizontalMediaCollectionView()
	{
		InitializeComponent();
	}

    protected MediaCollectionViewModel ViewModel { get => BindingContext as MediaCollectionViewModel; }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();        
    }

    internal async void BringToView(BaseServiceModel modelObject)
    {
        try
        {
            if (!MainThread.IsMainThread)
            {
                MainThread.BeginInvokeOnMainThread(() => { BringToView(modelObject); });
                return;
            }
            var item = ItemCollection.Children.FirstOrDefault(child => child.Handler is not null);
            var element = ItemCollection.Children
                .OfType<Element>()
                .OfType<MediaListItemView>()
                .FirstOrDefault(iv => (iv.BindingContext as BaseMediaListItem).Id == modelObject.Id);
            await Task.Delay(1000);
            if (element is not null)
                await ScrollContent.ScrollToAsync(element as Element, ScrollToPosition.Start, true);
        }
        catch(Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }
}