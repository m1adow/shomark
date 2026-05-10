using System.Text.Json;

namespace ShoMark.Analytics.Infrastructure.Providers;

internal static class JsonOptions
{
    internal static readonly JsonSerializerOptions Default = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
