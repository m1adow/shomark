using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        builder.ToTable("campaigns");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.FragmentId).HasColumnName("fragment_id");
        builder.Property(c => c.VideoId).HasColumnName("video_id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(255);
        builder.Property(c => c.TargetAudience).HasColumnName("target_audience").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(c => c.Description).HasColumnName("description").HasColumnType("text");
        builder.Property(c => c.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasOne(c => c.Fragment)
            .WithMany(f => f.Campaigns)
            .HasForeignKey(c => c.FragmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Video)
            .WithMany(v => v.Campaigns)
            .HasForeignKey(c => c.VideoId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.FragmentId);
        builder.HasIndex(c => c.VideoId);
        builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique();
    }
}
