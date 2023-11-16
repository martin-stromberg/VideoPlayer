using VideoPlayer.ViewModels.MediaLists;

namespace VideoPlayer.Views.MediaLists;

public partial class MediaList : ContentPage
{
	public MediaList()
	{
		InitializeComponent();
        BindingContext = ViewModel = App.GetService<MediaItemListViewModel>();
    }

    protected MediaItemListViewModel ViewModel { get; }


    protected override void OnAppearing()
    {
        base.OnAppearing();
        ViewModel.OnAppeared();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ViewModel.OnDisappeared(true);
    }
}