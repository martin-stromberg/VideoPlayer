using Mediathek.Services.MediaLibrary.Demo;

namespace MediaPlayer.Helper
{
    public static class UserSecretsExtension
    {

        public static MauiAppBuilder RegisterSecrets(this MauiAppBuilder builder)
        {
            builder.Services.AddSingleton<IUserSecrets, UserSecrets>();
            return builder;
        }

    }
}
