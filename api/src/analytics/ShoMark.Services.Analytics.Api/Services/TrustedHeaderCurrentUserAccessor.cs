using ShoMark.Common;

namespace ShoMark.Analytics.Api.Services;

public class TrustedHeaderCurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TrustedHeaderCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetUserId()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers[GatewayHeaderNames.UserId].ToString();
        return Guid.TryParse(header, out var id) ? id : Guid.Empty;
    }
}
