using ShoMark.Application.DTOs.Analytics;
using ShoMark.Application.DTOs.Campaigns;
using ShoMark.Application.DTOs.Fragments;
using ShoMark.Application.DTOs.Notifications;
using ShoMark.Application.DTOs.Platforms;
using ShoMark.Application.DTOs.Posts;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Domain.Entities;

namespace ShoMark.Application.Mappings;

public static class DtoMappingExtensions
{
    public static VideoDto ToDto(this Video v) => new(
        v.Id, v.Title, v.MinioKey, v.OriginalFileName,
        v.DurationSeconds, v.FileSize, v.CreatedAt, v.UpdatedAt);

    public static CampaignDto ToDto(this Campaign c) => new(
        c.Id, c.UserId, c.FragmentId, c.VideoId, c.Name,
        c.TargetAudience?.ToString(), c.Description,
        c.Status.ToString(), c.CreatedAt, c.UpdatedAt);

    public static NotificationDto ToDto(this Notification n) => new(
        n.Id, n.UserId, n.Type.ToString(), n.Title, n.Message,
        n.ReferenceId, n.IsRead, n.CreatedAt);

    public static AnalyticsDto ToDto(this Analytics a) => new(
        a.Id, a.PostId, a.Views, a.Likes, a.Shares, a.Comments,
        a.LastSyncedAt, a.CreatedAt, a.UpdatedAt);

    public static PostDto ToDto(this Post p) => new(
        p.Id, p.FragmentId, p.PlatformId, p.CampaignId, p.Title, p.Content, p.ExternalUrl,
        p.Status.ToString(), p.ScheduledAt, p.PublishedAt, p.CreatedAt, p.UpdatedAt);

    public static AiFragmentDto ToDto(this AiFragment f) => new(
        f.Id, f.VideoId, f.Description,
        f.StartTime, f.EndTime,
        f.MinioKey, f.CalculateViralScore(), f.Hashtags,
        f.ThumbnailKey, f.IsApproved, f.CreatedAt, f.UpdatedAt);

    public static PlatformDto ToDto(this Platform p) => new(
        p.Id, p.UserId, p.PlatformType.ToString(), p.AccountName,
        p.TokenExpiresAt, p.CreatedAt, p.UpdatedAt);

    internal static double CalculateViralScore(this AiFragment f)
    {
        double baseScore = f.ViralScore.HasValue ? f.ViralScore.Value * 10.0 : 5.0;

        double duration = f.EndTime - f.StartTime;
        double durationFactor = duration switch
        {
            < 10 => 0.7,
            < 15 => 0.85,
            < 30 => 1.0,
            < 60 => 0.95,
            < 120 => 0.8,
            _ => 0.6
        };

        double contentBonus = 0;
        if (!string.IsNullOrWhiteSpace(f.Description)) contentBonus += 0.5;
        if (!string.IsNullOrWhiteSpace(f.Hashtags))
        {
            var tagCount = f.Hashtags.Split([' ', ',', '#'], StringSplitOptions.RemoveEmptyEntries).Length;
            contentBonus += Math.Min(tagCount * 0.2, 1.0);
        }

        double score = (baseScore * durationFactor) + contentBonus;
        return Math.Round(Math.Clamp(score, 0, 10), 1);
    }
}
