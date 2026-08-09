using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using VideoWebPlayer.Components;
using VideoWebPlayer.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using msTools.Backup;

namespace VideoWebPlayer.Extensions;

/// <summary>
/// Extension methods for configuring the VideoWebPlayer application pipeline.
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Applies pending database migrations on application startup.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The same <see cref="WebApplication"/> instance.</returns>
    public static WebApplication MigrateDatabase(this WebApplication app)
    {
        // DB-Migration beim Start
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
            return app;
        }
    }
    /// <summary>
    /// Configures middleware and endpoints for the VideoWebPlayer application.
    /// </summary>
    /// <param name="app">The web application instance.</param>
    /// <returns>The same <see cref="WebApplication"/> instance.</returns>
    public static WebApplication UseVideoWebPlayer(this WebApplication app)
    {
        // Fehler-/Sicherheitskonfiguration
        if (app.Environment.IsDevelopment())
        {
            app.UseMigrationsEndPoint();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // Fr�h einschr�nken
        app.UseWhitelistIp();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseBackups();
        app.UseAntiforgery();


        app.MapRazorComponents<App>()
           .AddInteractiveServerRenderMode();

        app.MapControllers();
        
        // SignalR Hub (JWT-authentifiziert)
        app.MapHub<VideoWebPlayer.Hubs.MediaUpdateHub>("/hubs/mediaupdate")
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme).RequireAuthenticatedUser());

        // Identity /Account Razor Endpoints
        app.MapAdditionalIdentityEndpoints();

        return app;
    }
}