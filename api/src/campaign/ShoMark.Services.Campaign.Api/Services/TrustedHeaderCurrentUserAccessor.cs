using ShoMark.Application.Interfaces;
using ShoMark.Common;

namespace ShoMark.Api.Services;

public class TrustedHeaderCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TrustedHeaderCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private IHeaderDictionary Headers => _httpContextAccessor.HttpContext?.Request.Headers
        ?? throw new InvalidOperationException("No active HTTP context.");

    public bool IsAuthenticated => Headers.ContainsKey(GatewayHeaderNames.UserId);

    public Guid UserId
    {
        get
        {
            var value = Headers[GatewayHeaderNames.UserId].FirstOrDefault();
            return Guid.TryParse(value, out var userId)
                ? userId
                : throw new InvalidOperationException("Trusted gateway user header is missing or invalid.");
        }
    }

    public string Email => Headers[GatewayHeaderNames.Email].FirstOrDefault() ?? string.Empty;

    public string Name => Headers[GatewayHeaderNames.Name].FirstOrDefault() ?? string.Empty;
}
