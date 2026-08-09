using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using VideoWebPlayer.Client;
using VideoWebPlayer.Components.Account;
using VideoWebPlayer.Data;
using VideoWebPlayer.Services;
using VideoWebPlayer.Services.Authentication;
using VideoWebPlayer.Services.DemoData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using msTools.Backup;
using VideoWebPlayer.Services.Backups;
using VideoWebPlayer.ViewModels;

namespace VideoWebPlayer.Extensions;

/// <summary>
/// Extension methods for configuring VideoWebPlayer services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers VideoWebPlayer services and middleware dependencies.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <returns>The same builder instance.</returns>
    public static WebApplicationBuilder AddVideoWebPlayerServices(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;
        var env = builder.Environment;

        // Secrets laden
        var jwtKey = configuration["Jwt:Key"];
        // Versuche zuerst den Web-spezifischen Token, dann den allgemeinen
        var apiKey = configuration["Jwt:ApiToken:Web"] ?? configuration["Jwt:ApiToken"];
        var issuer = configuration["Jwt:Issuer"] ?? "VideoWebPlayer";

        // In Produktion sicherstellen, dass Secrets vorhanden sind
        if (env.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Fehlende Konfiguration: Jwt:Key");
            if (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException("Fehlende Konfiguration: Jwt:ApiToken oder Jwt:ApiToken:Web");
        }

        // Blazor/Identity
        services.AddRazorComponents().AddInteractiveServerComponents();
        services.AddServerSideBlazor().AddCircuitOptions(o => o.DetailedErrors = true);

        services.AddCascadingAuthenticationState();
        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireClaim("IsAdmin", "True"));
        });
        services.AddScoped<AuthorizationTokenService>();
        services.AddScoped<IdentityUserAccessor>();
        services.AddScoped<IdentityRedirectManager>();
        services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
        services.AddMemoryCache();
        services.AddSingleton<InternalConnectionService>();
        services.AddSingleton<ILoginIpBlockService, LoginIpBlockService>(); // wieder Singleton


        var authenticationBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        });

        authenticationBuilder.AddIdentityCookies();

        authenticationBuilder.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = issuer,
                IssuerSigningKey = string.IsNullOrWhiteSpace(jwtKey)
                    ? null
                    : new SymmetricSecurityKey(Convert.FromBase64String(jwtKey)),
                NameClaimType = ClaimTypes.NameIdentifier
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // SignalR sendet bei WebSockets token typischerweise als access_token in Query
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/mediaupdate"))
                    {
                        context.Token = accessToken;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = "VideoWebPlayer.Auth";

            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;

            // safer default for proxied deployments: use scheme of incoming request
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            options.Cookie.Path = "/";

            options.LoginPath = "/Account/Login";
            options.LogoutPath = "/Account/Logout";
            options.AccessDeniedPath = "/Account/AccessDenied";

            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
        });

        // Add antiforgery service (middleware will be configured in app startup)
        builder.Services.AddAntiforgery();

        // Optional: externe / 2FA Cookies ebenfalls anpassen
        //services.ConfigureExternalCookie(options =>
        //{
        //    options.Cookie.Name = "VideoWebPlayer.External";
        //    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        //});

        // Datenbank
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=app.db";
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        // HttpClient stabil via Factory
        services.AddHttpClient("Internal", (sp, http) =>
        {
            var connectionManager = sp.GetRequiredService<InternalConnectionService>();
            http.DefaultRequestHeaders.Add("User-Agent", connectionManager.GetUserAgent());
            if (!string.IsNullOrWhiteSpace(apiKey))
                http.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        });

        // BaseAddress erst im scoped Kontext bestimmen (Blazor-Circuit, HTTP-Request oder Fallback)
        services.AddScoped<HttpClient>(sp =>
        {
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Internal");

            var nav = sp.GetService<NavigationManager>();
            if (nav is not null)
            {
                http.BaseAddress = new Uri(nav.BaseUri);
                return http;
            }

            var httpContext = sp.GetService<IHttpContextAccessor>()?.HttpContext;
            if (httpContext is not null)
            {
                var req = httpContext.Request;
                http.BaseAddress = new Uri($"{req.Scheme}://{req.Host}{req.PathBase}/");
                return http;
            }

            var baseUrl = sp.GetService<IConfiguration>()?["App:BaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                http.BaseAddress = new Uri(baseUrl);
            }

            return http;
        });

        // Domaenenspezifische Services
        services.AddScoped<VideoWebPlayerClient>(sp =>
        {
            var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
            var client = new InternalVideoWebPlayerClient(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IHttpContextAccessor>(), 
                sp.GetRequiredService<UserManager<ApplicationUser>>(),
                sp.GetRequiredService<AuthorizationTokenService>(),
                sp.GetRequiredService<ILogger<VideoWebPlayerClient>>());
            return client;
        });

        services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
        services.AddSingleton<EventManager>();
        services.AddScoped<MediaSourceScanner>();
        services.AddScoped<MediaSourceClassifier>();
        services.AddScoped<IFavoritesService, FavoritesService>();
		services.AddScoped<IGenreService, GenreService>();
        services.AddScoped<SftpMediaSourceReader>();
        services.AddScoped<DataUpgradeManager>();
        services.AddScoped<ProgramSettingsService>();
        services.AddBackups(configuration.GetSection("Backups"));
        services.AddScoped<IBackupDataProvider, VideoWebPlayerBackupDataProvider>();
        services.AddSingleton<IBackgroundProcessingGate, BackgroundProcessingGate>();
        services.AddSingleton<IBackupRestoreGuard, VideoWebPlayerBackupRestoreGuard>();
        services.AddScoped<BackupSettingsService>();
        services.AddScoped<IBackupOptionsProvider, BackupSettingsService>();
        services.AddScoped<BackupOperationHistoryService>();
        services.AddScoped<IAutomaticBackupRunner, VideoWebPlayerAutomaticBackupRunner>();
        services.AddScoped<VideoWebPlayerBackupFacade>();
        services.AddSingleton<ManualBackupJobService>();
        services.AddSingleton<RestoreBackupJobService>();
        services.AddScoped<RecentEntryService>();
        services.AddTransient<IAuthService, AuthService>();
        services.AddHostedService<MediaSourceScanService>();
        services.AddHttpContextAccessor();
        services.AddControllers();
        services.AddSingleton<ContinueWatchingBuffer>();
        services.AddScoped<ContinueWatchingService>();
        services.AddSingleton<MediaUpdateNotificationService>();
        services.AddHostedService<ContinueWatchingWorker>();
		services.AddScoped<IDemoDataSetService, FileSystemDemoDataSetService>();

        services.AddScoped<MediaSourceDetailsViewModel>();
        
        // SignalR
        services.AddSignalR();

        // JWT-Signaturschluessel registrieren (Base64)
        if (!string.IsNullOrWhiteSpace(jwtKey))
        {
            services.AddSingleton(new SymmetricSecurityKey(Convert.FromBase64String(jwtKey)));
        }

        return builder;
    }
}
