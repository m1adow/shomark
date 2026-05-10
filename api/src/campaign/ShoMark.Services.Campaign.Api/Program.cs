using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using ShoMark.Api.Services;
using ShoMark.Application.Interfaces;
using ShoMark.Infrastructure.Data;
using ShoMark.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddCampaignInfrastructure(builder.Configuration);
builder.Services.AddCampaignApplicationServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserAccessor, TrustedHeaderCurrentUserAccessor>();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CampaignDbContext>();
    await db.Database.EnsureCreatedAsync();

    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("ShoMark Campaign API"));
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
