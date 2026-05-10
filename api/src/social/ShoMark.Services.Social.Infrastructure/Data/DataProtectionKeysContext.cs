using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ShoMark.Infrastructure.Data;

/// <summary>
/// Minimal DbContext used exclusively to persist ASP.NET Core Data Protection keys
/// in social_db, so the Analytics service can share the same key ring for token decryption.
/// Run `dotnet ef migrations add AddDataProtectionKeys --context DataProtectionKeysContext`
/// in ShoMark.Services.Social.Infrastructure to create the DataProtectionKeys table.
/// </summary>
public class DataProtectionKeysContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeysContext(DbContextOptions<DataProtectionKeysContext> options)
        : base(options) { }

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
}
