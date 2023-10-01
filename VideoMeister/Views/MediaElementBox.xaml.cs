using VideoMeister.ViewModels.Navigation;

namespace VideoMeister.Views;

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
}