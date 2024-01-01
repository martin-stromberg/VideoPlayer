namespace FolderAPI.Services
{
    public static class ServicesExtension
    {
        public static IServiceCollection RegisterServices(this IServiceCollection services)
        {
            services.AddSingleton<FileManager>();
            return services;
        }
    }
}
