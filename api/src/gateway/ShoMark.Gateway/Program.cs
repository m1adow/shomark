using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using ShoMark.Common;

const long MaxVideoUploadBytes = 2L * 1024 * 1024 * 1024;
const long MultipartUploadHeadroomBytes = 64L * 1024 * 1024;
const long MaxMultipartUploadRequestBytes = MaxVideoUploadBytes + MultipartUploadHeadroomBytes;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxMultipartUploadRequestBytes;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var authority = builder.Configuration["Keycloak:Authority"]!;
        var validIssuer = builder.Configuration["Keycloak:ValidIssuer"];

        options.Authority = authority;
        options.RequireHttpsMetadata = builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", false);
        options.TokenValidationParameters.ValidIssuer = string.IsNullOrWhiteSpace(validIssuer)
            ? authority
            : validIssuer;
        options.TokenValidationParameters.ValidateAudience = false;

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                var isSseEndpoint =
                    (path.StartsWithSegments("/api/videos") && path.Value?.EndsWith("/events") == true) ||
                    path.StartsWithSegments("/api/notifications/stream");

                if (isSseEndpoint && context.Request.Query.TryGetValue("access_token", out var token))
                {
                    context.Token = token;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (IsOAuthCallback(context.Request.Path))
    {
        await next();
        return;
    }

    if (context.User.Identity?.IsAuthenticated != true)
    {
        await context.ChallengeAsync();
        return;
    }

    var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? context.User.FindFirstValue("sub");

    if (!Guid.TryParse(userId, out _))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    context.Request.Headers[GatewayHeaderNames.UserId] = userId;
    context.Request.Headers[GatewayHeaderNames.Email] =
        context.User.FindFirstValue(ClaimTypes.Email) ?? context.User.FindFirstValue("email") ?? string.Empty;
    context.Request.Headers[GatewayHeaderNames.Name] =
        context.User.FindFirstValue("name")
        ?? context.User.FindFirstValue(ClaimTypes.Name)
        ?? context.User.FindFirstValue("preferred_username")
        ?? string.Empty;

    await next();
});

app.MapReverseProxy();

app.Run();

static bool IsOAuthCallback(PathString path) =>
    path.StartsWithSegments("/api/oauth") &&
    path.Value?.EndsWith("/callback", StringComparison.OrdinalIgnoreCase) == true;
