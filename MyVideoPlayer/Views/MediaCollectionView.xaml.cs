using MyVideoPlayer.ViewModels.Navigation;

namespace MyVideoPlayer.Views;

public partial class MediaCollectionView : ContentView
{
    public MediaCollectionView()
    {
        InitializeComponent();
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        ((sender as Button).BindingContext as BaseMediaElementBoxViewModel).ProcessTapped();
    }
}