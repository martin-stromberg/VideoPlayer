using WebPlayerApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
var memoryLoggerProvider = new InMemoryLoggerProvider();
builder.Logging.ClearProviders();
builder.Logging.AddProvider(memoryLoggerProvider);
builder.Services.AddSingleton(memoryLoggerProvider);
builder.Services.AddSingleton<IMediaService, MediaService>();
builder.Services.AddTransient<IMediaCache, MediaCache>();
builder.Services.AddSingleton<ISourceService, SourceService>();
builder.Services.AddHostedService<MediaDirectoryHostedService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();
app.UseHttpsRedirection();

app.UseAuthorization();
app.UseMiddleware<IPCheck>();

app.MapControllers();

app.Run();
