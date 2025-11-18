using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using tp_aspire_samy_jugurtha.WebApp.Clients;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.AddServiceDefaults();

var keysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_PATH") 
    ?? Path.Combine(Path.GetTempPath(), "workly-keys");

Directory.CreateDirectory(keysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("workly-web");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "oidc";
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookieOptions =>
    {
        cookieOptions.Cookie.SameSite = SameSiteMode.Lax;
        cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.None;
        cookieOptions.Cookie.HttpOnly = true;
        cookieOptions.Cookie.Path = "/";
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = builder.Configuration["Authentication:OIDC:Authority"];
        options.ClientId = builder.Configuration["Authentication:OIDC:ClientId"];
        options.RequireHttpsMetadata = false;
        options.ResponseType = "code";
        options.ResponseMode = "query";
        options.SaveTokens = true;
        options.UsePkce = true;

        options.CallbackPath = "/signin-oidc";
        options.SignedOutCallbackPath = "/signout-callback-oidc";

        options.CorrelationCookie.SameSite = SameSiteMode.Lax;
        options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.None;
        options.CorrelationCookie.HttpOnly = true;
        options.CorrelationCookie.IsEssential = true;
        
        options.NonceCookie.SameSite = SameSiteMode.Lax;
        options.NonceCookie.SecurePolicy = CookieSecurePolicy.None;
        options.NonceCookie.HttpOnly = true;
        options.NonceCookie.IsEssential = true;

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "realm_access.roles"
        };

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("api");
        options.Scope.Add("roles");

        options.ClaimActions.MapJsonKey("role", "realm_access.roles");
        
        // Extraction des rôles depuis le token Keycloak
        options.Events.OnTokenValidated = context =>
        {
            var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
            if (claimsIdentity != null)
            {
                var realmAccessClaim = context.Principal?.FindFirst("realm_access");
                if (realmAccessClaim != null)
                {
                    var realmAccess = System.Text.Json.JsonDocument.Parse(realmAccessClaim.Value);
                    if (realmAccess.RootElement.TryGetProperty("roles", out var rolesElement))
                    {
                        foreach (var role in rolesElement.EnumerateArray())
                        {
                            claimsIdentity.AddClaim(new System.Security.Claims.Claim(
                                claimsIdentity.RoleClaimType ?? "role",
                                role.GetString() ?? ""
                            ));
                        }
                    }
                }
            }
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("user", "admin"));
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<TokenHandler>();

builder.Services.AddHttpClient<IWorklyClient, WorklyClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Services:ApiBaseUrl"] ?? "https+http://apiservice";
    client.BaseAddress = new Uri(apiBaseUrl);
    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
}).AddHttpMessageHandler<TokenHandler>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

app.UseCookiePolicy(new CookiePolicyOptions
{
    MinimumSameSitePolicy = SameSiteMode.Unspecified,
    Secure = CookieSecurePolicy.None
});

app.UseAuthentication();
app.UseAuthorization();

// Connexion via OIDC
app.MapGet("/authentication/login", async ctx =>
{
    var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/";
    await ctx.ChallengeAsync("oidc", new AuthenticationProperties { RedirectUri = returnUrl });
}).AllowAnonymous();

// Inscription via Keycloak
app.MapGet("/authentication/register", ctx =>
{
    var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
    var authority = configuration["Authentication:OIDC:Authority"];
    var clientId = configuration["Authentication:OIDC:ClientId"];
    var redirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/signin-oidc";
    var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
    var registerUrl = $"{authority}/protocol/openid-connect/registrations?client_id={clientId}&response_type=code&scope=openid&redirect_uri={encodedRedirectUri}";
    
    ctx.Response.Redirect(registerUrl);
    return Task.CompletedTask;
}).AllowAnonymous();

// Déconnexion complète (cookie + Keycloak SSO)
app.MapGet("/authentication/logout", async ctx =>
{
    var configuration = ctx.RequestServices.GetRequiredService<IConfiguration>();
    
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    
    var authority = configuration["Authentication:OIDC:Authority"];
    var redirectUri = $"{ctx.Request.Scheme}://{ctx.Request.Host}/";
    var encodedRedirectUri = Uri.EscapeDataString(redirectUri);
    var clientId = configuration["Authentication:OIDC:ClientId"];
    var keycloakLogoutUrl = $"{authority}/protocol/openid-connect/logout?post_logout_redirect_uri={encodedRedirectUri}&client_id={clientId}";
    
    ctx.Response.Redirect(keycloakLogoutUrl);
}).AllowAnonymous();

app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

// Ajoute automatiquement le token Bearer aux requêtes API
public class TokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _http;

    public TokenHandler(IHttpContextAccessor http) => _http = http;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var ctx = _http.HttpContext;
        if (ctx is not null)
        {
            var accessToken = await ctx.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }
        return await base.SendAsync(request, ct);
    }
}
