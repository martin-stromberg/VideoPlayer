using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using WebPlayer.Client.Services;
using WebPlayer.Components;
using WebPlayer.Components.Account;
using WebPlayer.Data;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddTransient<IUserCollection, FileUserStore>();
builder.Services.AddScoped<IdentityUserAccessor>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();
builder.Services.AddHttpClient("My.ServerAPI", client =>
{
    var baseAddress = config.GetConnectionString("api");
    client.BaseAddress = new Uri(baseAddress); // oder deine API-URL
});
builder.Services.AddHttpClient("Own", client =>
{
    var baseAddress = config.GetConnectionString("own");
    client.BaseAddress = new Uri(baseAddress); // oder deine API-URL
});

builder.Services.AddTransient<IAPIClient, APIClient>();
builder.Services.AddTransient<IServiceAPIClient, ServiceAPIClient>();
builder.Services.AddScoped<IMediaDirectoryAccessApi, MediaDirectoryAccessApi>();

builder.Services.AddScoped<IUserCollection, FileUserStore>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Server API", Version = "v1" });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

//builder.Services.AddSingleton<IUserStore<ApplicationUser>, FileUserStore>();
//builder.Services.AddSingleton<IRoleStore<ApplicationRole>, FileRoleStore>();

//builder.Services.AddIdentity<ApplicationUser, ApplicationRole>()
//        .AddDefaultTokenProviders();

builder.Services
    .AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<ApplicationRole>()
    .AddUserStore<FileUserStore>()
    .AddRoleStore<FileRoleStore>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();    
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseMigrationsEndPoint();
app.UseSwagger();
app.UseSwaggerUI(c =>
  c.SwaggerEndpoint("/swagger/v1/swagger.json", "Server API v1"));

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.EndsWith(".css"))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
    }
});
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WebPlayer.Client._Imports).Assembly);

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

app.Run();
