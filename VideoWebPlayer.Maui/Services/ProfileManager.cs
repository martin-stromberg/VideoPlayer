namespace VideoWebPlayer.Maui.Services;

/// <summary>
/// Manages application profiles for different launch configurations.
/// Each profile has its own database and isolated settings.
/// </summary>
public class ProfileManager
{
    private static ProfileManager? _instance;
    private string _currentProfile = "default";
    
    public static ProfileManager Instance => _instance ??= new ProfileManager();
    
    public string CurrentProfile
    {
        get => _currentProfile;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                value = "default";
            _currentProfile = value.ToLowerInvariant();
            System.Diagnostics.Debug.WriteLine($"[ProfileManager] Profile changed to: {_currentProfile}");
        }
    }
    
    public string GetDatabaseFileName()
    {
        return CurrentProfile == "default" 
            ? "downloads.db3" 
            : $"downloads_{CurrentProfile}.db3";
    }
    
    public string GetPreferencesPrefix()
    {
        return CurrentProfile == "default" 
            ? "" 
            : $"{CurrentProfile}_";
    }
    
    public ProfileManager()
    {
        // Versuche das Profil aus Umgebungsvariable zu laden
        var profileEnv = Environment.GetEnvironmentVariable("VIDEOWEBPLAYER_PROFILE");
        if (!string.IsNullOrWhiteSpace(profileEnv))
        {
            CurrentProfile = profileEnv;
        }
    }
}
