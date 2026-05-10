using Scalar.AspNetCore;
using ShoMark.Analytics.Api.Services;
using ShoMark.Analytics.Infrastructure.DependencyInjection;
using ShoMark.Analytics.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAnalyticsInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<TrustedHeaderCurrentUserAccessor>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var analyticsDb = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
    await analyticsDb.Database.EnsureCreatedAsync();

    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("ShoMark Analytics API"));
}

app.Use(async (context, next) =>
{
    if (IsDeveloperEndpoint(context.Request.Path))
    {
        await next();
        return;
    }

    if (!context.Request.Headers.ContainsKey("X-User-Id"))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }

    await next();
});

app.MapControllers();

app.Run();

static bool IsDeveloperEndpoint(PathString path) =>
    path.StartsWithSegments("/openapi") || path.StartsWithSegments("/scalar");
