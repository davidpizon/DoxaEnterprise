using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Relational DB (PostgreSQL) wired via Aspire — connection string "doxadb"
// is injected by the AppHost; EF Core + health checks + telemetry auto-added.
builder.AddNpgsqlDbContext<DoxaDbContext>("doxadb");

// Distributed cache.
builder.AddRedisDistributedCache("cache");

// Stateless JWT bearer auth — validates tokens against the Keycloak "doxa"
// realm JWKS (discovered via the "keycloak" service reference).
builder.Services.AddAuthentication()
    .AddKeycloakJwtBearer(
        serviceName: "keycloak",
        realm: "doxa",
        configureOptions: options =>
        {
            options.Audience = "doxa-api";
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidateIssuer = true;
            options.TokenValidationParameters.RoleClaimType = "roles";
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("doxa.admin", policy => policy.RequireRole("doxa-admin"));
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Public endpoint.
app.MapGet("/api/status", () => Results.Ok(new { service = "doxa-api", status = "ok" }))
   .AllowAnonymous();

// Protected — requires a valid bearer token.
app.MapGet("/api/profile", (ClaimsPrincipal user) =>
        Results.Ok(new
        {
            name = user.Identity?.Name,
            claims = user.Claims.Select(c => new { c.Type, c.Value })
        }))
   .RequireAuthorization();

// Admin-only.
app.MapGet("/api/admin/metrics", () => Results.Ok(new { tenants = 42 }))
   .RequireAuthorization("doxa.admin");

app.MapDefaultEndpoints();

app.Run();

// Minimal EF Core context for illustration.
public sealed class DoxaDbContext(DbContextOptions<DoxaDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
}

public sealed class Tenant
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

// Exposes the implicit Program class so integration tests can use
// WebApplicationFactory<Program>. See Doxa.ApiService.Tests.
public partial class Program;
