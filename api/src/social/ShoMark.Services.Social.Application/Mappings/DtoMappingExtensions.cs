using ShoMark.Application.DTOs.Analytics;
using ShoMark.Application.DTOs.Platforms;
using ShoMark.Application.DTOs.Posts;
using ShoMark.Domain.Entities;

namespace ShoMark.Application.Mappings;

public static class DtoMappingExtensions
{
    public static AnalyticsDto ToDto(this Analytics a) => new(
        a.Id, a.PostId, a.Views, a.Likes, a.Shares, a.Comments,
        a.LastSyncedAt, a.CreatedAt, a.UpdatedAt);

    public static PostDto ToDto(this Post p) => new(
        p.Id, p.FragmentId, p.PlatformId, p.CampaignId, p.Title, p.Content, p.ExternalUrl,
        p.Status.ToString(), p.ScheduledAt, p.PublishedAt, p.CreatedAt, p.UpdatedAt);

    public static PlatformDto ToDto(this Platform p) => new(
        p.Id, p.UserId, p.PlatformType.ToString(), p.AccountName,
        p.TokenExpiresAt, p.CreatedAt, p.UpdatedAt);
}
