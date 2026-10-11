using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using MinGo.StaticPages.Application.Dtos;
using MinGo.StaticPages.Application.Services;
using MinGo.StaticPages.Domain.Entities;
using MinGo.StaticPages.Infrastructure;
using MinGo.StaticPages.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Console logs: SimpleConsole, single line (replace default console provider)
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.IncludeScopes = false;
    o.TimestampFormat = "HH:mm:ss ";
    o.UseUtcTimestamp = false;
});

builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 512L * 1024 * 1024;
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Smart";
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddPolicyScheme("Smart", "Cookie or API Key", options =>
{
    options.ForwardDefaultSelector = ctx =>
    {
        var hasKey = ctx.Request.Headers.ContainsKey("X-API-Key")
            || (ctx.Request.Headers.Authorization.FirstOrDefault()?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true
                && !ctx.Request.Headers.Authorization.First()!.Contains("cookie", StringComparison.OrdinalIgnoreCase));
        // API key sent as raw Bearer token (not cookie session)
        var authHeader = ctx.Request.Headers.Authorization.FirstOrDefault();
        var isBearerKey = !string.IsNullOrEmpty(authHeader)
            && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            && authHeader.Length > 20;
        return hasKey || isBearerKey
            ? MinGo.StaticPages.Web.Auth.ApiKeyDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    };
})
.AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, MinGo.StaticPages.Web.Auth.ApiKeyAuthenticationHandler>(
    MinGo.StaticPages.Web.Auth.ApiKeyDefaults.AuthenticationScheme, _ => { })
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=mingo-static.db";
builder.Services.AddMinGoInfrastructure(connectionString);

// HttpClient BaseAddress follows the current request host (no hardcoded URL).
// Prefer HttpContext (works for SSR/API), fall back to NavigationManager (interactive circuit).
// Cookie/Authorization from the inbound request are forwarded so SSR API calls authenticate.
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<MinGo.StaticPages.Web.Auth.UserAgentAuthForwardingHandler>();
builder.Services.AddScoped(sp =>
{
    var httpContext = sp.GetRequiredService<IHttpContextAccessor>().HttpContext;
    Uri baseUri;
    if (httpContext is not null)
    {
        var req = httpContext.Request;
        baseUri = new Uri($"{req.Scheme}://{req.Host}{req.PathBase}/");
    }
    else
    {
        var nav = sp.GetService<Microsoft.AspNetCore.Components.NavigationManager>();
        var uri = nav?.BaseUri;
        if (!string.IsNullOrEmpty(uri))
            baseUri = new Uri(uri);
        else
            baseUri = new Uri("http://localhost/");
    }

    var handler = sp.GetRequiredService<MinGo.StaticPages.Web.Auth.UserAgentAuthForwardingHandler>();
    handler.InnerHandler = new HttpClientHandler { UseCookies = false };
    return new HttpClient(handler) { BaseAddress = baseUri };
});

var app = builder.Build();

// Ensure database + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
    var hasher = scope.ServiceProvider.GetRequiredService<MinGo.StaticPages.Application.Abstractions.IPasswordHasher>();
    if (!await db.Users.AnyAsync())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = hasher.Hash("admin123"),
            Role = RoleNames.Admin
        });
        await db.SaveChangesAsync();
    }
    if (!await db.StorageSettings.AnyAsync())
    {
        db.StorageSettings.Add(new StorageSettings
        {
            Provider = StorageProviderKind.Local,
            LocalRoot = Path.GetFullPath("storage-data"),
            Bucket = "static-pages",
            Region = "us-east-1",
            ForcePathStyle = true
        });
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();

app.MapRazorComponents<MinGo.StaticPages.Web.Components.App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MinGo.StaticPages.Web.Client._Imports).Assembly);

app.MapGet("/healthz", () => Results.Ok(new { status = "ok", time = DateTimeOffset.UtcNow }))
    .WithTags("System");

// ---------- Auth ----------
// Browser form login: Set-Cookie is applied to the real browser request.
app.MapPost("/auth/login", async (HttpContext http, AuthService auth) =>
{
    var form = await http.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    var user = await auth.ValidateCredentialsAsync(username, password);
    if (user is null)
        return Results.Redirect("/login?error=1&returnUrl=" + Uri.EscapeDataString(returnUrl));

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role),
        new("auth_scheme", "cookie")
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));

    var target = !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//")
        ? returnUrl
        : "/dashboard";
    return Results.Redirect(target);
}).AllowAnonymous().DisableAntiforgery();

app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
}).AllowAnonymous().DisableAntiforgery();

// JSON login (CI / API clients)
app.MapPost("/api/auth/login", async (LoginRequest req, AuthService auth, HttpContext http) =>
{
    var user = await auth.ValidateCredentialsAsync(req.Username, req.Password);
    if (user is null)
        return Results.Unauthorized();

    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, user.Username),
        new(ClaimTypes.Role, user.Role),
        new("auth_scheme", "cookie")
    };
    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    await http.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity));
    return Results.Ok(new LoginResponse(user.Username, user.Role));
}).WithTags("Auth").AllowAnonymous();

app.MapPost("/api/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
}).WithTags("Auth");

app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
{
    if (user.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();
    return Results.Ok(new LoginResponse(
        user.Identity!.Name ?? "",
        user.FindFirstValue(ClaimTypes.Role) ?? RoleNames.Viewer));
}).WithTags("Auth");

// ---------- API Keys ----------
app.MapPost("/api/apikeys", async (CreateApiKeyRequest req, AuthService auth, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    var created = await auth.CreateApiKeyAsync(req, user.Identity?.Name ?? "admin");
    return Results.Ok(created);
}).WithTags("Auth").RequireAuthorization();

app.MapGet("/api/apikeys", async (AuthService auth, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    return Results.Ok(await auth.ListApiKeysAsync());
}).WithTags("Auth").RequireAuthorization();

app.MapDelete("/api/apikeys/{id:guid}", async (Guid id, AuthService auth, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    await auth.RevokeApiKeyAsync(id);
    return Results.NoContent();
}).WithTags("Auth").RequireAuthorization();

// ---------- Projects ----------
app.MapPost("/api/projects", async (CreateProjectRequest req, ProjectService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    try
    {
        var dto = await svc.CreateAsync(req);
        return Results.Created($"/api/projects/{dto.Id}", dto);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Projects").RequireAuthorization();

app.MapGet("/api/projects", async (ProjectService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    return Results.Ok(await svc.ListAsync());
}).WithTags("Projects").RequireAuthorization();

app.MapGet("/api/projects/{id:guid}", async (Guid id, ProjectService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}).WithTags("Projects").RequireAuthorization();

app.MapDelete("/api/projects/{id:guid}", async (Guid id, ProjectService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) is not (RoleNames.Admin or RoleNames.Publisher))
        return Results.Forbid();
    try
    {
        await svc.DeleteAsync(id);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Projects").RequireAuthorization();

// ---------- Versions ----------
app.MapPost("/api/projects/{id:guid}/versions", async (Guid id, HttpRequest http, VersionService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) is not (RoleNames.Admin or RoleNames.Publisher))
        return Results.Forbid();

    if (!http.HasFormContentType)
        return Results.Problem("multipart/form-data required", statusCode: 400);

    var form = await http.ReadFormAsync();
    var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
    if (file is null)
        return Results.Problem("file is required", statusCode: 400);

    var request = new UploadVersionRequest(
        form["number"].ToString(),
        form["commitSha"].ToString(),
        form["branch"].ToString(),
        form["message"].ToString(),
        SpaEnabled: !string.Equals(form["spaEnabled"], "false", StringComparison.OrdinalIgnoreCase),
        SpaFallback: string.IsNullOrWhiteSpace(form["spaFallback"]) ? "index.html" : form["spaFallback"].ToString());

    try
    {
        await using var stream = file.OpenReadStream();
        var dto = await svc.UploadAsync(id, stream, request, user.Identity?.Name ?? "api");
        return Results.Created($"/api/versions/{dto.Id}", dto);
    }
    catch (ZipValidationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Versions").RequireAuthorization();

app.MapGet("/api/projects/{id:guid}/versions", async (Guid id, VersionService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    return Results.Ok(await svc.ListAsync(id));
}).WithTags("Versions").RequireAuthorization();

app.MapGet("/api/versions/{id:guid}", async (Guid id, VersionService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    var dto = await svc.GetAsync(id);
    return dto is null ? Results.NotFound() : Results.Ok(dto);
}).WithTags("Versions").RequireAuthorization();

app.MapDelete("/api/versions/{id:guid}", async (Guid id, VersionService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) is not (RoleNames.Admin or RoleNames.Publisher))
        return Results.Forbid();
    try
    {
        await svc.DeleteAsync(id);
        return Results.NoContent();
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Versions").RequireAuthorization();

// ---------- Releases ----------
app.MapPost("/api/projects/{id:guid}/publish", async (Guid id, PublishRequest req, ReleaseService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) is not (RoleNames.Admin or RoleNames.Publisher))
        return Results.Forbid();
    try
    {
        var dto = await svc.PublishAsync(id, req, user.Identity?.Name ?? "api");
        return Results.Ok(dto);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Releases").RequireAuthorization();

app.MapPost("/api/projects/{id:guid}/rollback", async (Guid id, RollbackRequest req, ReleaseService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) is not (RoleNames.Admin or RoleNames.Publisher))
        return Results.Forbid();
    try
    {
        var dto = await svc.RollbackAsync(id, req, user.Identity?.Name ?? "api");
        return Results.Ok(dto);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Releases").RequireAuthorization();

app.MapGet("/api/projects/{id:guid}/releases", async (Guid id, ReleaseService svc, ClaimsPrincipal user) =>
{
    if (!IsManager(user))
        return Results.Forbid();
    return Results.Ok(await svc.HistoryAsync(id));
}).WithTags("Releases").RequireAuthorization();

// ---------- Resolve (data plane) ----------
app.MapGet("/api/resolve/{projectName}", async (
    string projectName,
    string? channel,
    string? version,
    ResolveService svc,
    HttpContext http,
    MinGo.StaticPages.Application.Abstractions.IStorageSettingsService settingsService,
    AuthService auth) =>
{
    var settings = await settingsService.GetAsync();
    if (settings.ResolveRequireAuth)
    {
        if (!await IsApiKeyOrUserAsync(http, auth))
            return Results.Unauthorized();
    }

    var result = await svc.ResolveAsync(projectName, channel, version);
    return result is null ? Results.NotFound(new { error = "not_published_or_missing" }) : Results.Ok(result);
}).WithTags("Resolve").AllowAnonymous();

// ---------- Storage settings ----------
app.MapGet("/api/settings/storage", async (MinGo.StaticPages.Application.Abstractions.IStorageSettingsService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    var s = await svc.GetAsync();
    return Results.Ok(new StorageSettingsDto(
        s.Provider.ToString(),
        s.ServiceUrl,
        s.Region,
        s.AccessKey,
        s.Bucket,
        s.Prefix,
        s.ForcePathStyle,
        s.LocalRoot,
        s.MaxPackageBytes,
        s.MaxFileCount,
        s.MaxSingleFileBytes,
        s.ResolveRequireAuth,
        s.PublishedCacheSeconds,
        s.VersionCacheSeconds));
}).WithTags("Settings").RequireAuthorization();

app.MapPut("/api/settings/storage", async (UpdateStorageSettingsRequest req, MinGo.StaticPages.Application.Abstractions.IStorageSettingsService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    try
    {
        var provider = Enum.Parse<StorageProviderKind>(req.Provider, ignoreCase: true);
        var updated = await svc.UpdateAsync(new StorageSettings
        {
            Provider = provider,
            ServiceUrl = req.ServiceUrl,
            Region = req.Region,
            AccessKey = req.AccessKey,
            SecretKey = req.SecretKey,
            Bucket = req.Bucket,
            Prefix = req.Prefix,
            ForcePathStyle = req.ForcePathStyle,
            LocalRoot = req.LocalRoot,
            MaxPackageBytes = req.MaxPackageBytes,
            MaxFileCount = req.MaxFileCount,
            MaxSingleFileBytes = req.MaxSingleFileBytes,
            ResolveRequireAuth = req.ResolveRequireAuth,
            PublishedCacheSeconds = req.PublishedCacheSeconds,
            VersionCacheSeconds = req.VersionCacheSeconds
        });
        return Results.Ok(new StorageSettingsDto(
            updated.Provider.ToString(),
            updated.ServiceUrl,
            updated.Region,
            updated.AccessKey,
            updated.Bucket,
            updated.Prefix,
            updated.ForcePathStyle,
            updated.LocalRoot,
            updated.MaxPackageBytes,
            updated.MaxFileCount,
            updated.MaxSingleFileBytes,
            updated.ResolveRequireAuth,
            updated.PublishedCacheSeconds,
            updated.VersionCacheSeconds));
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        return Results.Problem(ex.Message, statusCode: 400);
    }
}).WithTags("Settings").RequireAuthorization();

app.MapPost("/api/settings/storage/test", async (MinGo.StaticPages.Application.Abstractions.IStorageSettingsService svc, ClaimsPrincipal user) =>
{
    if (user.FindFirstValue(ClaimTypes.Role) != RoleNames.Admin)
        return Results.Forbid();
    var ok = await svc.TestConnectivityAsync();
    return Results.Ok(new { ok });
}).WithTags("Settings").RequireAuthorization();

app.Run();

static bool IsManager(ClaimsPrincipal user) =>
    user.FindFirstValue(ClaimTypes.Role) is RoleNames.Admin or RoleNames.Publisher or RoleNames.Viewer;

static async Task<bool> IsApiKeyOrUserAsync(HttpContext http, AuthService auth)
{
    var header = http.Request.Headers.Authorization.FirstOrDefault();
    string? key = null;
    if (!string.IsNullOrEmpty(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        key = header["Bearer ".Length..].Trim();
    if (string.IsNullOrEmpty(key) && http.Request.Headers.TryGetValue("X-API-Key", out var x))
        key = x.ToString();

    if (!string.IsNullOrEmpty(key))
    {
        var (ok, _) = await auth.ValidateApiKeyAsync(key);
        return ok;
    }

    return http.User.Identity?.IsAuthenticated == true;
}

public partial class Program;
