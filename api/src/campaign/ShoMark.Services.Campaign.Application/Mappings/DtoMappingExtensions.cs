using ShoMark.Application.DTOs.Campaigns;
using ShoMark.Application.DTOs.Fragments;
using ShoMark.Application.DTOs.Videos;
using ShoMark.Domain.Entities;

namespace ShoMark.Application.Mappings;

public static class DtoMappingExtensions
{
    public static VideoDto ToDto(this Video v) => new(
        v.Id, v.Title, v.StorageKey, v.OriginalFileName,
        v.DurationSeconds, v.FileSize, v.Summary, v.CreatedAt, v.UpdatedAt);

    public static CampaignDto ToDto(this Campaign c) => new(
        c.Id, c.UserId, c.FragmentId, c.VideoId, c.Name,
        c.TargetAudience?.ToString(), c.Description,
        c.Status.ToString(), c.CreatedAt, c.UpdatedAt);

    public static AiFragmentDto ToDto(this AiFragment f) => new(
        f.Id, f.VideoId, f.Description,
        f.StartTime, f.EndTime,
        f.StorageKey, f.CalculateViralScore(), f.Hashtags,
        f.ThumbnailKey, f.IsApproved, f.CreatedAt, f.UpdatedAt);

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
