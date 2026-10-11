using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MinGo.StaticPages.Web.Client.Auth;

public sealed class CookieAuthStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _http;

    public CookieAuthStateProvider(HttpClient http)
    {
        _http = http;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/auth/me");
            if (!response.IsSuccessStatusCode)
                return Anonymous();

            var me = await response.Content.ReadFromJsonAsync<MeDto>();
            if (me is null || string.IsNullOrEmpty(me.Username))
                return Anonymous();

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, me.Username),
                new(ClaimTypes.Role, me.Role)
            };
            var identity = new ClaimsIdentity(claims, "cookie");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return Anonymous();
        }
    }

    private static AuthenticationState Anonymous()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity("anonymous"));
        return new AuthenticationState(principal);
    }

    private sealed record MeDto(string Username, string Role);
}
