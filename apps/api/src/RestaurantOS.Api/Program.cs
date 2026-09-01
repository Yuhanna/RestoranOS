using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using RestaurantOS.Api;
using RestaurantOS.Api.Infrastructure;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEmailSender(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddSingleton(TimeProvider.System);
var managementAuth = builder.Configuration
    .GetRequiredSection(ManagementAuthOptions.SectionName)
    .Get<ManagementAuthOptions>()
    ?? throw new InvalidOperationException("ManagementAuth configuration is required.");
if (Encoding.UTF8.GetByteCount(managementAuth.SigningKey) < 32)
{
    throw new InvalidOperationException(
        "ManagementAuth:SigningKey must be supplied securely and contain at least 32 UTF-8 bytes.");
}

builder.Services.Configure<ManagementAuthOptions>(
    builder.Configuration.GetRequiredSection(ManagementAuthOptions.SectionName));
builder.Services.Configure<CustomerWebOptions>(
    builder.Configuration.GetSection(CustomerWebOptions.SectionName));
builder.Services.AddDataProtection();
builder.Services.AddScoped<IPasswordHasher<ManagementUser>, PasswordHasher<ManagementUser>>();
builder.Services.AddScoped<IManagementAuthService, ManagementAuthService>();
builder.Services.AddScoped<DevelopmentManagementBootstrapper>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments(
                        "/hubs/v1/management-orders"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = managementAuth.Issuer,
            ValidateAudience = true,
            ValidAudience = managementAuth.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(managementAuth.SigningKey)),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.NameIdentifier,
        };
    });
builder.Services.AddAuthorization(ManagementPolicies.AddManagementPolicies);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(
        "management-login",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimits:ManagementLogin:PermitLimit", 5),
                Window = TimeSpan.FromMinutes(
                    builder.Configuration.GetValue("RateLimits:ManagementLogin:WindowMinutes", 1)),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
    options.AddPolicy(
        "management-refresh",
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = builder.Configuration.GetValue("RateLimits:ManagementRefresh:PermitLimit", 20),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});
var signalR = builder.Services.AddSignalR();
var redisBackplane = builder.Configuration.GetConnectionString("SignalRRedis");
if (!string.IsNullOrWhiteSpace(redisBackplane))
{
    signalR.AddStackExchangeRedis(redisBackplane);
}
builder.Services.AddSingleton<IOrderStatusNotifier, SignalROrderStatusNotifier>();
builder.Services.AddSingleton<IManagementOrderNotifier, SignalRManagementOrderNotifier>();
builder.Services.AddSingleton<IManagementNotificationNotifier, SignalRManagementNotificationNotifier>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    var configured = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    var allowLanInDevelopment = builder.Environment.IsDevelopment()
        && builder.Configuration.GetValue("Cors:AllowLanInDevelopment", false);
    policy
        .SetIsOriginAllowed(origin =>
        {
            if (configured.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            if (!allowLanInDevelopment || !Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme is not ("http" or "https"))
            {
                return false;
            }

            if (uri.IsLoopback)
            {
                return true;
            }

            // Development-only: phone/tablet on the same LAN (RFC1918).
            if (!System.Net.IPAddress.TryParse(uri.Host, out var ip))
            {
                return false;
            }

            var bytes = ip.GetAddressBytes();
            return ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && (bytes[0] == 10
                    || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                    || (bytes[0] == 192 && bytes[1] == 168));
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
}));
builder.Services
    .AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddDbContextCheck<RestaurantOsDbContext>(tags: ["ready"]);

var app = builder.Build();

var mediaRoot = Path.Combine(app.Environment.ContentRootPath, "media");
Directory.CreateDirectory(mediaRoot);

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
// CORS must run before HTTPS redirection so browser preflight (and LAN HTTP clients)
// are not bounced to https://localhost which phones cannot open.
app.UseCors();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(mediaRoot),
    RequestPath = "/media",
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
    });
app.MapHub<CustomerOrderHub>("/hubs/v1/customer-orders");
app.MapHub<ManagementOrderHub>("/hubs/v1/management-orders");

if (app.Environment.IsDevelopment())
{
    await using (var migrateScope = app.Services.CreateAsyncScope())
    {
        var db = migrateScope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        if (db.Database.IsRelational())
        {
                await db.Database.MigrateAsync();
        }
    }

    await using (var bootstrapScope = app.Services.CreateAsyncScope())
    {
        await bootstrapScope.ServiceProvider
            .GetRequiredService<DevelopmentManagementBootstrapper>()
            .RunAsync(CancellationToken.None);
    }
}

if (args.Contains("--bootstrap-management-admin", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<DevelopmentManagementBootstrapper>()
        .RunAsync(CancellationToken.None);
    return;
}

app.Run();

public partial class Program;
