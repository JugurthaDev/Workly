using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace tp_aspire_samy_jugurtha.WebApp.Testing;

public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string Scheme = "Test";

    public TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock) : base(options, logger, encoder, clock) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Par défaut, utilisateur simple; possibilité d'injecter des rôles via en-tête si nécessaire
        var rolesHeader = Request.Headers.ContainsKey("X-Roles") ? Request.Headers["X-Roles"].ToString() : "user";
        var roles = rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, "e2e-user"),
            new Claim(ClaimTypes.Name, "e2e-user")
        };
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var identity = new ClaimsIdentity(claims, Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
