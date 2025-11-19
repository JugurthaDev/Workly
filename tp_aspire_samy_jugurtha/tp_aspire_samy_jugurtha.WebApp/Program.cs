using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Headers;
using System.Security.Claims;
using tp_aspire_samy_jugurtha.WebApp.Clients;
using tp_aspire_samy_jugurtha.WebApp.Models;
using tp_aspire_samy_jugurtha.WebApp.Testing;

var app = Program.CreateApp(args);
app.Run();

public partial class Program
{
    public static WebApplication CreateApp(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ApplicationName = typeof(Program).Assembly.GetName().Name
        });

        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.AddServiceDefaults();

        var keysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_PATH")
            ?? Path.Combine(Path.GetTempPath(), "workly-keys");

        Directory.CreateDirectory(keysPath);

        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName("workly-web")
            .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

        var useTestAuth = IsTruthy(Environment.GetEnvironmentVariable("E2E_TEST_AUTH"));
        if (useTestAuth)
        {
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
                options.AddPolicy("UserOrAdmin", policy => policy.RequireRole(Roles.User, Roles.Admin));
            });
        }
        else
        {
            var isDevelopment = builder.Environment.IsDevelopment();
            
            builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = "oidc";
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, cookieOptions =>
                {
                    cookieOptions.Cookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
                    cookieOptions.Cookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                    cookieOptions.Cookie.HttpOnly = true;
                    cookieOptions.Cookie.Path = "/";
                    cookieOptions.Cookie.IsEssential = true;
                })
                .AddOpenIdConnect("oidc", options =>
                {
                    options.Authority = builder.Configuration["Authentication:OIDC:Authority"];
                    options.ClientId = builder.Configuration["Authentication:OIDC:ClientId"];
                    options.RequireHttpsMetadata = !isDevelopment;
                    options.ResponseType = "code";
                    options.ResponseMode = "query";
                    options.SaveTokens = true;
                    options.UsePkce = true;
                    options.GetClaimsFromUserInfoEndpoint = true;

                    options.CallbackPath = "/signin-oidc";
                    options.SignedOutCallbackPath = "/signout-callback-oidc";

                    options.CorrelationCookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
                    options.CorrelationCookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                    options.CorrelationCookie.HttpOnly = true;
                    options.CorrelationCookie.IsEssential = true;
                    options.CorrelationCookie.Path = "/";

                    options.NonceCookie.SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None;
                    options.NonceCookie.SecurePolicy = isDevelopment ? CookieSecurePolicy.None : CookieSecurePolicy.Always;
                    options.NonceCookie.HttpOnly = true;
                    options.NonceCookie.IsEssential = true;
                    options.NonceCookie.Path = "/";

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

                    // Désactive l'usage de request_uri / PAR côté client si injecté par défaut
                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        if (context.ProtocolMessage.Parameters.ContainsKey("request_uri"))
                        {
                            context.ProtocolMessage.Parameters.Remove("request_uri");
                        }
                        if (context.ProtocolMessage.Parameters.ContainsKey("request"))
                        {
                            context.ProtocolMessage.Parameters.Remove("request");
                        }
                        
                        // Si c'est une demande d'inscription, modifie l'URL pour pointer vers l'endpoint de registration Keycloak
                        if (context.Properties.Items.TryGetValue("kc_action", out var action) && action == "register")
                        {
                            // Remplace /auth par /registrations dans l'URL d'autorisation
                            var issuer = context.ProtocolMessage.IssuerAddress;
                            if (!string.IsNullOrEmpty(issuer) && issuer.Contains("/protocol/openid-connect/auth"))
                            {
                                context.ProtocolMessage.IssuerAddress = issuer.Replace(
                                    "/protocol/openid-connect/auth",
                                    "/protocol/openid-connect/registrations"
                                );
                            }
                        }
                        
                        return Task.CompletedTask;
                    };
                });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminOnly", policy => policy.RequireRole(Roles.Admin));
                options.AddPolicy("UserOrAdmin", policy => policy.RequireRole(Roles.User, Roles.Admin));
            });
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<TokenHandler>();

    var useFakeClient = IsTruthy(Environment.GetEnvironmentVariable("E2E_FAKE_CLIENT"));
    var runningInAspire = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL"))
                  || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL"));
        if (useFakeClient)
        {
            builder.Services.AddSingleton<IWorklyClient, FakeWorklyClient>();
        }
        else
        {
            builder.Services.AddHttpClient<IWorklyClient, WorklyClient>(client =>
            {
                // Lorsque l'app tourne sous AppHost (Aspire), on utilise la découverte de services (https+http)
                // Sinon on lit depuis la configuration (fallback localhost en dev pur)
                var apiBaseUrl = runningInAspire
                    ? "https+http://apiservice"
                    : (builder.Configuration["Services:ApiBaseUrl"] ?? "http://localhost:5018");
                client.BaseAddress = new Uri(apiBaseUrl);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            }).AddHttpMessageHandler<TokenHandler>();
        }

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStaticFiles();
        app.UseRouting();

        var isProduction = !app.Environment.IsDevelopment();
        app.UseCookiePolicy(new CookiePolicyOptions
        {
            MinimumSameSitePolicy = SameSiteMode.Unspecified,
            Secure = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.None,
            HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always
        });

        app.UseAuthentication();
        app.UseAuthorization();

        if (!useTestAuth)
        {
            // Connexion via OIDC (désactivée en E2E)
            app.MapGet("/authentication/login", async ctx =>
            {
                var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/dashboard";
                await ctx.ChallengeAsync("oidc", new AuthenticationProperties { RedirectUri = returnUrl });
            }).AllowAnonymous();

            // Inscription via Keycloak (passe par le middleware OIDC vers l'endpoint de registration)
            app.MapGet("/authentication/register", async ctx =>
            {
                var returnUrl = ctx.Request.Query["returnUrl"].FirstOrDefault() ?? "/dashboard";
                var properties = new AuthenticationProperties 
                { 
                    RedirectUri = returnUrl
                };
                // Marque cette demande comme une inscription pour modifier l'URL Keycloak
                properties.Items["kc_action"] = "register";
                
                await ctx.ChallengeAsync("oidc", properties);
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
        }

        app.MapRazorPages();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        return app;
    }

    private static bool IsTruthy(string? value)
        => !string.IsNullOrEmpty(value) && (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));
}

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
