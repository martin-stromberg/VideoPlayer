namespace FolderAPI.Middleware
{
    public static class MiddlewareExtensions
    {
        public static WebApplication RegisterMiddlewares(this WebApplication app)
        {
            app.UseMiddleware<HeaderChecker>();
            return app;
        }
    }
}
