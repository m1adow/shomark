using Microsoft.EntityFrameworkCore;
using ShoMark.Analytics.Domain.Entities;

namespace ShoMark.Analytics.Infrastructure.Persistence;

public class AnalyticsDbContext : DbContext
{
    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options) { }

    public DbSet<PostMetricSnapshot> PostMetricSnapshots => Set<PostMetricSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PostMetricSnapshot>(e =>
        {
            e.ToTable("post_metric_snapshots");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.PostId).HasColumnName("post_id").IsRequired();
            e.Property(s => s.Views).HasColumnName("views");
            e.Property(s => s.Likes).HasColumnName("likes");
            e.Property(s => s.Shares).HasColumnName("shares");
            e.Property(s => s.Comments).HasColumnName("comments");
            e.Property(s => s.SyncedAt).HasColumnName("synced_at");

            e.HasIndex(s => new { s.PostId, s.SyncedAt });
        });
    }
}
