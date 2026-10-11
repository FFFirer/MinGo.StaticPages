namespace MinGo.StaticPages.Web.Auth;

/// <summary>
/// Forwards the inbound request's Cookie/Authorization headers onto
/// server-side HttpClient calls made during Blazor prerender, so
/// same-origin /api/* requests are authenticated like the browser.
/// </summary>
public sealed class UserAgentAuthForwardingHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var http = httpContextAccessor.HttpContext;
        if (http is not null)
        {
            var cookie = http.Request.Headers.Cookie.ToString();
            if (!string.IsNullOrEmpty(cookie) && !request.Headers.Contains("Cookie"))
                request.Headers.TryAddWithoutValidation("Cookie", cookie);

            var auth = http.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(auth) && !request.Headers.Contains("Authorization"))
                request.Headers.TryAddWithoutValidation("Authorization", auth);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
