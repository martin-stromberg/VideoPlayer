namespace VideoWebPlayer.Maui
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            // Register Shell routes used for navigation
            Routing.RegisterRoute("home", typeof(HomePage));

            // Dynamically populate sources into the flyout menu after services are available
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try
                {
                    // wait a short moment for DI to be set
                    await Task.Delay(200);
                    var client = App.ServiceProvider?.GetService<VideoWebPlayer.Client.VideoWebPlayerClient>();
                    if (client == null)
                        return;

                    var sources = await client.RequestSourcesAsync();
                    if (sources == null)
                        return;

                    foreach (var src in sources)
                    {
                        var id = src.Id;
                        var name = src.Name ?? $"Quelle {id}";
                        var menu = new MenuItem { Text = name };
                        menu.Clicked += async (s, e) =>
                        {
                            try
                            {
                                await Shell.Current.GoToAsync("//MainPage");
                                var page = App.ServiceProvider?.GetService<SourceOverviewPage>() ?? new SourceOverviewPage(id, name);
                                await Shell.Current.Navigation.PushAsync(page);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[AppShell] Error opening source {id}: {ex.Message}");
                            }
                        };
                        this.Items.Add(menu);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[AppShell] Error populating source menu: {ex.Message}");
                }
            });
        }

        private async void OnShellSettingsClicked(object sender, EventArgs e)
        {
            try
            {
                var settings = App.ServiceProvider?.GetService<Services.ISettingsService>();
                var page = App.ServiceProvider?.GetService<SettingsPage>() ?? new SettingsPage(settings);
                await Shell.Current.GoToAsync("//MainPage");
                await Shell.Current.Navigation.PushAsync(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Error opening settings from shell: {ex.Message}");
            }
        }

        private async void OnShellSourcesClicked(object sender, EventArgs e)
        {
            try
            {
                await Shell.Current.GoToAsync("//MainPage");
                var page = App.ServiceProvider?.GetService<SourceOverviewPage>() ?? new SourceOverviewPage(0, "Quellen");
                await Shell.Current.Navigation.PushAsync(page);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AppShell] Error opening sources from shell: {ex.Message}");
            }
        }
    }
}
