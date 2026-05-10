using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class AnalyticsConfiguration : IEntityTypeConfiguration<Analytics>
{
    public void Configure(EntityTypeBuilder<Analytics> builder)
    {
        builder.ToTable("analytics");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(a => a.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(a => a.Views).HasColumnName("views").HasDefaultValue(0);
        builder.Property(a => a.Likes).HasColumnName("likes").HasDefaultValue(0);
        builder.Property(a => a.Shares).HasColumnName("shares").HasDefaultValue(0);
        builder.Property(a => a.Comments).HasColumnName("comments").HasDefaultValue(0);
        builder.Property(a => a.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        // 1:1 relationship with Post
        builder.HasOne(a => a.Post)
            .WithOne(p => p.Analytics)
            .HasForeignKey<Analytics>(a => a.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.PostId).IsUnique();
    }
}
