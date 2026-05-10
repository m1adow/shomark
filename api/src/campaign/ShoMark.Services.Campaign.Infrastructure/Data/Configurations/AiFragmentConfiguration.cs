using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class AiFragmentConfiguration : IEntityTypeConfiguration<AiFragment>
{
    public void Configure(EntityTypeBuilder<AiFragment> builder)
    {
        builder.ToTable("ai_fragments");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(f => f.VideoId).HasColumnName("video_id").IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(f => f.StartTime).HasColumnName("start_time").IsRequired();
        builder.Property(f => f.EndTime).HasColumnName("end_time").IsRequired();
        builder.Property(f => f.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
        builder.Property(f => f.ViralScore).HasColumnName("viral_score");
        builder.Property(f => f.Hashtags).HasColumnName("hashtags").HasMaxLength(1000);
        builder.Property(f => f.ThumbnailKey).HasColumnName("thumbnail_key").HasMaxLength(500);
        builder.Property(f => f.IsApproved).HasColumnName("is_approved").HasDefaultValue(false);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasOne(f => f.Video)
            .WithMany(v => v.Fragments)
            .HasForeignKey(f => f.VideoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.VideoId);
    }
}
