using Microsoft.Maui.Controls;

namespace VideoWebPlayer.Maui;

public partial class LoadingPage : ContentPage
{
    public LoadingPage(string message)
    {
        InitializeComponent();
        MessageLabel.Text = message;
    }
}
