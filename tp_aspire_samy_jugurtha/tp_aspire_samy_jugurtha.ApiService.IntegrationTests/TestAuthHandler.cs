using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Xunit;

namespace tp_aspire_samy_jugurtha.ApiService.IntegrationTests;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public const string AuthScheme = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IHttpContextAccessor httpContextAccessor) : base(options, logger, encoder, clock)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var headers = httpContext?.Request.Headers;

        var user = headers != null && headers.TryGetValue("X-User", out var u) ? u.ToString() : "test-user";
        var rolesHeader = headers != null && headers.TryGetValue("X-Roles", out var r) ? r.ToString() : string.Empty;
        var roles = string.IsNullOrWhiteSpace(rolesHeader) ? Array.Empty<string>() : rolesHeader.Split(',');

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user),
            new Claim(ClaimTypes.Name, user)
        };
        // Minimal APIs use RoleClaimType by default for [Authorize(Roles=..)] => ClaimTypes.Role is fine here
        foreach (var role in roles.Select(x => x.Trim()).Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, AuthScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, AuthScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
