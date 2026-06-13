using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Distributed cache (Redis) — also backs server-side token storage so tokens
// never live in the browser and survive across app instances.
builder.AddRedisDistributedCache("cache");

const string OidcScheme = "DoxaOidc";

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OidcScheme;
        options.DefaultSignOutScheme = OidcScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.None; // required for OIDC redirects
        options.SlidingExpiration = true;
    })
    // Aspire resolves the Keycloak authority from the "keycloak" service ref
    // and the "doxa" realm; serviceName + realm => https+http://keycloak/realms/doxa
    .AddKeycloakOpenIdConnect(
        serviceName: "keycloak",
        realm: "doxa",
        authenticationScheme: OidcScheme,
        configureOptions: options =>
        {
            options.ClientId = "doxa-web";
            options.ClientSecret = builder.Configuration["Oidc:ClientSecret"]; // injected by AppHost
            options.ResponseType = OpenIdConnectResponseType.Code;             // auth-code + PKCE
            options.UsePkce = true;

            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.Scope.Add("doxa-api");       // audience for the backend API
            options.Scope.Add("offline_access"); // refresh tokens

            // Securely store tokens in the encrypted auth cookie so the
            // backchannel can use the access token to call the API.
            options.SaveTokens = true;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            options.TokenValidationParameters.NameClaimType = "preferred_username";
            options.TokenValidationParameters.RoleClaimType = "roles";

            // Backchannel Logout: Keycloak POSTs a logout token to this path
            // when the user signs out at the IdP or another client.
            options.RemoteSignOutPath = "/signout-oidc";
            options.SignedOutCallbackPath = "/signout-callback-oidc";
        });

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Typed client to the backend API; resilience handlers come from ServiceDefaults.
builder.Services.AddHttpClient("DoxaApi", client =>
{
    client.BaseAddress = new("https+http://apiservice");
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Login / logout endpoints (front-channel).
app.MapGet("/login", (string? returnUrl) =>
    TypedResults.Challenge(new() { RedirectUri = returnUrl ?? "/" }))
   .AllowAnonymous();

app.MapPost("/logout", () =>
    TypedResults.SignOut(
        new() { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OidcScheme]));

app.MapDefaultEndpoints();

// Map your root Razor component once you add one, e.g.:
// app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.MapGet("/", () => "Doxa frontend is running. Wire up Razor components here.")
   .AllowAnonymous();

app.Run();
