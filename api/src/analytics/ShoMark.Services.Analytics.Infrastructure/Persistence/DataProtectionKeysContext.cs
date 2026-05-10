using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShoMark.Analytics.Infrastructure.Persistence;

/// <summary>
/// Read/write context for the DataProtectionKeys table in social_db.
/// Shares the same key ring as the Social service, allowing the Analytics service
/// to decrypt platform OAuth tokens stored encrypted by the Social service.
/// </summary>
public class DataProtectionKeysContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysContext(DbContextOptions<DataProtectionKeysContext> options)
        : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
