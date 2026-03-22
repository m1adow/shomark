using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShoMark.Domain.Entities;

namespace ShoMark.Infrastructure.Data.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(p => p.FragmentId).HasColumnName("fragment_id").IsRequired();
        builder.Property(p => p.PlatformId).HasColumnName("platform_id").IsRequired();
        builder.Property(p => p.Title).HasColumnName("title").HasMaxLength(500);
        builder.Property(p => p.Content).HasColumnName("content").HasColumnType("text");
        builder.Property(p => p.ExternalUrl).HasColumnName("external_url").HasMaxLength(1000);
        builder.Property(p => p.Status).HasColumnName("status").HasMaxLength(20)
            .HasConversion<string>();
        builder.Property(p => p.ScheduledAt).HasColumnName("scheduled_at");
        builder.Property(p => p.PublishedAt).HasColumnName("published_at");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");

        builder.HasOne(p => p.Fragment)
            .WithMany(f => f.Posts)
            .HasForeignKey(p => p.FragmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Platform)
            .WithMany(pl => pl.Posts)
            .HasForeignKey(p => p.PlatformId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => p.FragmentId);
        builder.HasIndex(p => p.PlatformId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ScheduledAt);
    }
}
