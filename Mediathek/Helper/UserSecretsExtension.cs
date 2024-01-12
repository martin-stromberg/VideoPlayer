using Mediathek.Services.MediaLibrary.Demo;

namespace Mediathek.Helper
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
