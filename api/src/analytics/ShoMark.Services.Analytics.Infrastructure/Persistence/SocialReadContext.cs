using Microsoft.EntityFrameworkCore;
using ShoMark.Analytics.Domain.Entities;

namespace ShoMark.Analytics.Infrastructure.Persistence;

/// <summary>
/// Read-only EF context that maps to social_db tables.
/// The Analytics service never writes to this database.
/// </summary>
public class SocialReadContext : DbContext
{
    public SocialReadContext(DbContextOptions<SocialReadContext> options) : base(options) { }

    public DbSet<PostReadModel> Posts => Set<PostReadModel>();
    public DbSet<PlatformReadModel> Platforms => Set<PlatformReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlatformReadModel>(e =>
        {
            e.ToTable("platforms");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.PlatformType).HasColumnName("platform_type").HasMaxLength(20);
            e.Property(p => p.AccountName).HasColumnName("account_name").HasMaxLength(255);
            e.Property(p => p.AccessToken).HasColumnName("access_token").HasMaxLength(2000);
            e.Property(p => p.RefreshToken).HasColumnName("refresh_token").HasMaxLength(2000);
            e.Property(p => p.TokenExpiresAt).HasColumnName("token_expires_at");
        });

        modelBuilder.Entity<PostReadModel>(e =>
        {
            e.ToTable("posts");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id");
            e.Property(p => p.CampaignId).HasColumnName("campaign_id");
            e.Property(p => p.PlatformId).HasColumnName("platform_id");
            e.Property(p => p.Title).HasColumnName("title").HasMaxLength(500);
            e.Property(p => p.ExternalPostId).HasColumnName("external_post_id").HasMaxLength(500);
            e.Property(p => p.Status).HasColumnName("status").HasMaxLength(20);
            e.Property(p => p.PublishedAt).HasColumnName("published_at");
            e.Property(p => p.ScheduledAt).HasColumnName("scheduled_at");

            e.HasOne(p => p.Platform)
                .WithMany(pl => pl.Posts)
                .HasForeignKey(p => p.PlatformId);
        });
    }
}
