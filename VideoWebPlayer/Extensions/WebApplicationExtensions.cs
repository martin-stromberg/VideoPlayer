using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using VideoWebPlayer.Components;
using VideoWebPlayer.Data;

namespace VideoWebPlayer.Extensions;

public static class WebApplicationExtensions
{
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

        // Früh einschränken
        app.UseWhitelistIp();

        app.UseStaticFiles();
        app.UseAntiforgery();


        app.MapRazorComponents<App>()
           .AddInteractiveServerRenderMode();

        app.MapControllers();

        // Identity /Account Razor Endpoints
        app.MapAdditionalIdentityEndpoints();

        return app;
    }
}