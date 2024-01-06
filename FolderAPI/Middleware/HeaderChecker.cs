using System.Net;

namespace FolderAPI.Middleware
{
    public class HeaderChecker
    {
        private readonly RequestDelegate _next;
        private readonly string[] apiKeys = 
        { 
            "e568205d-f5ae-4754-954f-c0f56a266078", // Video-App
        };

        public HeaderChecker(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            bool isLocal = context.Request.Host.Host == "localhost";
            if (!context.Request.Headers.ContainsKey("X-ApiKey") && !isLocal)
                 throw new ArgumentNullException("X-ApiKey");
            var apiKey = context.Request.Headers["X-ApiKey"].SingleOrDefault();
            if (!apiKeys.Contains(apiKey) && !isLocal)
                 throw new ArgumentException("X-ApiKey");
            await _next(context);
        }
    }
}
