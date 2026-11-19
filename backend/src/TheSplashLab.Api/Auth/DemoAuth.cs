using System.Security.Claims;
using TheSplashLab.Core.Enums;
using TheSplashLab.Core.Interfaces;
using TheSplashLab.Infrastructure.Data;
using TheSplashLab.Infrastructure.Seed;

namespace TheSplashLab.Api.Auth;

/// <summary>
/// Local-demo identity switching. Not Microsoft Entra authentication.
/// </summary>
public class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserAccessor(IHttpContextAccessor http) => _http = http;

    private ClaimsPrincipal? Principal => _http.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : DemoSeed.EmployeeId;

    public string DisplayName => Principal?.FindFirstValue(ClaimTypes.Name) ?? "Alex Morgan";
    public string Email => Principal?.FindFirstValue(ClaimTypes.Email) ?? "alex.morgan@splashlab.demo";
    public string Role => Principal?.FindFirstValue(ClaimTypes.Role) ?? UserRole.Employee.ToString();
    public bool IsReviewer => Role is nameof(UserRole.Reviewer) or nameof(UserRole.Administrator);
    public bool IsAdministrator => Role == nameof(UserRole.Administrator);
}

public class DemoAuthMiddleware
{
    public const string HeaderName = "X-Demo-User-Id";
    private readonly RequestDelegate _next;

    public DemoAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var userIdHeader = context.Request.Headers[HeaderName].FirstOrDefault();
        var userId = Guid.TryParse(userIdHeader, out var parsed) ? parsed : DemoSeed.ReviewerId;

        var user = await db.Users.FindAsync(userId) ?? await db.Users.FindAsync(DemoSeed.ReviewerId);
        if (user is not null)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("department", user.Department)
            };
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "DemoAuth"));
        }

        await _next(context);
    }
}

public static class AuthExtensions
{
    public static IServiceCollection AddDemoAuth(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddAuthentication("DemoAuth")
            .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DemoAuthHandler>("DemoAuth", _ => { });
        services.AddAuthorization(options =>
        {
            options.AddPolicy("Reviewer", p => p.RequireRole(nameof(UserRole.Reviewer), nameof(UserRole.Administrator)));
            options.AddPolicy("Admin", p => p.RequireRole(nameof(UserRole.Administrator)));
        });
        return services;
    }
}

/// <summary>No-op handler; DemoAuthMiddleware already populates HttpContext.User.</summary>
public class DemoAuthHandler : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>
{
    public DemoAuthHandler(
        Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        Microsoft.Extensions.Logging.ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(Context.User, "DemoAuth");
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(ticket));
        }

        return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
    }
}
