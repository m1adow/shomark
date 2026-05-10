using Microsoft.EntityFrameworkCore;
using ShoMark.Analytics.Domain.Entities;

namespace ShoMark.Analytics.Infrastructure.Persistence;

/// <summary>
/// Read-only EF context that maps to campaigns_db tables.
/// The Analytics service never writes to this database.
/// </summary>
public class CampaignsReadContext : DbContext
{
    public CampaignsReadContext(DbContextOptions<CampaignsReadContext> options) : base(options) { }

    public DbSet<CampaignReadModel> Campaigns => Set<CampaignReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampaignReadModel>(e =>
        {
            e.ToTable("campaigns");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id");
            e.Property(c => c.UserId).HasColumnName("user_id");
            e.Property(c => c.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(c => c.Status).HasColumnName("status").HasMaxLength(20);
        });
    }
}
