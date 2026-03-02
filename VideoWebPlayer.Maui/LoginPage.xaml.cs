using Microsoft.Extensions.Configuration;
using Microsoft.Maui.Controls;
using VideoWebPlayer.Maui.Services;

namespace VideoWebPlayer.Maui;

public partial class LoginPage : ContentPage
{
    private readonly IAuthService _auth;

    public LoginPage(IAuthService? auth)
    {
        InitializeComponent();
        // tolerate null auth service when constructed outside DI
        _auth = auth ?? new Services.AuthService();

        if (_auth.HasCredentials())
        {
            // prefill
            // not exposing stored password in this placeholder
            UserEntry.Text = Microsoft.Maui.Storage.Preferences.Default.Get("Username", string.Empty);
        }
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var user = UserEntry.Text?.Trim();
        var pass = PasswordEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            StatusLabel.Text = "Bitte Benutzername und Passwort eingeben.";
            return;
        }

        StatusLabel.Text = "Anmeldung...";
        var ok = await _auth.LoginAsync(user!, pass!);
        if (ok)
        {
            await Navigation.PopModalAsync();
        }
        else
        {
            StatusLabel.Text = "Anmeldung fehlgeschlagen.";
        }
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        // allow user to exit the app
        System.Environment.Exit(0);
    }
}
