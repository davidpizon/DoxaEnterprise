var builder = DistributedApplication.CreateBuilder(args);

// ──────────────────────────────────────────────────────────────────────────
// Secure parameters — never hardcoded. Sourced from user secrets (dev),
// environment variables, or Key Vault (prod). secret:true keeps them out of
// the manifest and masks them in the dashboard.
// ──────────────────────────────────────────────────────────────────────────
var postgresPassword = builder.AddParameter("postgres-password", secret: true);
var keycloakAdmin = builder.AddParameter("keycloak-admin", secret: true);
var keycloakPassword = builder.AddParameter("keycloak-admin-password", secret: true);

// OIDC client secret for the confidential Web (frontend) client.
var oidcClientSecret = builder.AddParameter("doxa-web-client-secret", secret: true);

// ──────────────────────────────────────────────────────────────────────────
// Data tier: PostgreSQL (relational) + Redis (distributed cache / token store)
// ──────────────────────────────────────────────────────────────────────────
var postgres = builder.AddPostgres("postgres", password: postgresPassword)
    .WithDataVolume()                 // persist across restarts
    .WithPgAdmin()                    // dev-only admin UI (auto-excluded in publish)
    .WithLifetime(ContainerLifetime.Persistent);

var doxaDb = postgres.AddDatabase("doxadb");

var cache = builder.AddRedis("cache")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent);

// ──────────────────────────────────────────────────────────────────────────
// Identity Provider: Keycloak (OIDC). Realm + clients + roles imported from
// realms/doxa-realm.json so the IdP is reproducible across environments.
// ──────────────────────────────────────────────────────────────────────────
var keycloak = builder.AddKeycloak("keycloak", adminUsername: keycloakAdmin, adminPassword: keycloakPassword)
    .WithDataVolume()
    .WithRealmImport("../realms")
    // The realm import references ${DOXA_WEB_CLIENT_SECRET}; supply it from the
    // same parameter the web frontend uses so both ends of the OIDC client match.
    .WithEnvironment("DOXA_WEB_CLIENT_SECRET", oidcClientSecret)
    .WithLifetime(ContainerLifetime.Persistent);

// ──────────────────────────────────────────────────────────────────────────
// Backend API — validates JWT bearer tokens issued by Keycloak.
// ──────────────────────────────────────────────────────────────────────────
var apiService = builder.AddProject<Projects.Doxa_ApiService>("apiservice")
    .WithReference(doxaDb)
    .WithReference(cache)
    .WithReference(keycloak)
    // Aspire launches projects directly (no launchSettings) so they default to
    // Production. Forward the AppHost's environment so IsDevelopment()-gated
    // logic (e.g. RequireHttpsMetadata) behaves correctly against the
    // http-only Aspire Keycloak authority in local dev.
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
    .WaitFor(doxaDb)
    .WaitFor(cache)
    .WaitFor(keycloak)
    .WithExternalHttpEndpoints();

// ──────────────────────────────────────────────────────────────────────────
// Frontend Web — OIDC confidential client, server-side token storage in Redis,
// calls the API with the user's access token.
// ──────────────────────────────────────────────────────────────────────────
builder.AddProject<Projects.Doxa_Web>("webfrontend")
    .WithReference(cache)
    .WithReference(keycloak)
    .WithReference(apiService)
    .WithEnvironment("Oidc__ClientSecret", oidcClientSecret)   // secret injected as env var
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName) // forward Dev env (see apiservice)
    .WaitFor(cache)
    .WaitFor(keycloak)
    .WaitFor(apiService)
    .WithExternalHttpEndpoints();

builder.Build().Run();
