namespace VideoMeister;

public partial class App : Application
{
	public App(IServiceProvider serviceProvider)
    {
		InitializeComponent();

		MainPage = new AppShell();
        ServiceProvider = serviceProvider;
    }

    public IServiceProvider ServiceProvider { get; }

    internal static T GetService<T>()
    {
        return ((App)App.Current).ServiceProvider.GetService<T>();
    }
}
