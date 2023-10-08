using MyVideoPlayer.ViewModels.Navigation;

namespace MyVideoPlayer.Views;

public partial class MediaElementBox : ContentView
{
    public MediaElementBox()
    {
        InitializeComponent();
    }

    private void TapGestureRecognizer_Tapped(object sender, TappedEventArgs e)
    {
        (BindingContext as BaseMediaElementBoxViewModel).ProcessTapped();
    }

    private void MenuFlyoutItem_Clicked(object sender, EventArgs e)
    {
        switch ((sender as MenuFlyoutItem).CommandParameter)
        {
            case "load":
                (BindingContext as BaseMediaElementBoxViewModel).ProcessDownload();
                break;
        }
    }

    private void Button_Clicked(object sender, EventArgs e)
    {
        (BindingContext as BaseMediaElementBoxViewModel).ProcessTapped();
    }
}