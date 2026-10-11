using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MinGo.StaticPages.Application.Services;

namespace MinGo.StaticPages.Web.Auth;

public static class ApiKeyDefaults
{
    public const string AuthenticationScheme = "ApiKey";
}

public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AuthService auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? key = null;

        var header = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            key = header["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(key) && Request.Headers.TryGetValue("X-API-Key", out var x))
            key = x.ToString();

        if (string.IsNullOrEmpty(key))
            return AuthenticateResult.NoResult();

        var (ok, role) = await auth.ValidateApiKeyAsync(key);
        if (!ok)
            return AuthenticateResult.Fail("Invalid API key");

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "api-key"),
            new(ClaimTypes.Role, role),
            new("auth_scheme", "apikey")
        };
        var identity = new ClaimsIdentity(claims, ApiKeyDefaults.AuthenticationScheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyDefaults.AuthenticationScheme);
        return AuthenticateResult.Success(ticket);
    }
}
