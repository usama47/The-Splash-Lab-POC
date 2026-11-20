using Microsoft.EntityFrameworkCore;
using TheSplashLab.Api.Auth;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Infrastructure.Ai;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Integrations;
using TheSplashLab.Infrastructure.Repositories;
using TheSplashLab.Infrastructure.Seed;
using TheSplashLab.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "The Splash Lab POC API", Version = "v1" });
});

var configuredPath = builder.Configuration["Database:Path"];
var dbPath = string.IsNullOrWhiteSpace(configuredPath)
    ? Path.Combine(builder.Environment.ContentRootPath, "Data", "splashlab-poc.db")
    : configuredPath;
var dbDir = Path.GetDirectoryName(dbPath);
if (!string.IsNullOrWhiteSpace(dbDir))
    Directory.CreateDirectory(dbDir);

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddDemoAuth();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
var corsOriginsScalar = builder.Configuration["Cors:AllowedOrigins"];
if (corsOrigins.Length == 0 && !string.IsNullOrWhiteSpace(corsOriginsScalar))
    corsOrigins = new[] { corsOriginsScalar };
if (corsOrigins.Length == 1 && corsOrigins[0].Contains(',', StringComparison.Ordinal))
{
    corsOrigins = corsOrigins[0]
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
if (corsOrigins.Length == 0)
{
    corsOrigins = new[] { "http://localhost:3000", "http://127.0.0.1:3000" };
}

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("frontend", p => p
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddScoped<IMeetingSource, MeetingSource>();
builder.Services.AddScoped<IKnowledgeRepository, KnowledgeRepository>();
builder.Services.AddScoped<IRegisterRepository, RegisterRepository>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ITaskDestination, LocalTaskDestination>();
builder.Services.AddSingleton<IMicrosoftGraphIntegration, MicrosoftGraphIntegrationStub>();
builder.Services.AddSingleton<MockAiExtractionService>();
builder.Services.AddHttpClient<ClaudeAiExtractionService>();
builder.Services.AddScoped<IAiExtractionService, ConfigurableAiExtractionService>();
builder.Services.AddScoped<MeetingWorkflowService>();

builder.Services.ConfigureHttpJsonOptions(_ => { });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DemoSeed.EnsureSeededAsync(db);
}

if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", true))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseMiddleware<DemoAuthMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    mode = "MODE A local demo",
    utc = DateTime.UtcNow
}));

app.Run();

public partial class Program { }
