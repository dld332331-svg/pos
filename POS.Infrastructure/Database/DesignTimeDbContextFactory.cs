using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace POS.Infrastructure.Database;

/// <summary>
/// Design-time factory for POSDbContext, used by `dotnet ef migrations` CLI commands.
/// Uses the standard LocalDB connection for development migration generation.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<POSDbContext>
{
    public POSDbContext CreateDbContext(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var optionsBuilder = new DbContextOptionsBuilder<POSDbContext>();

        var connectionString = args.Length > 0
            ? args[0]
            : @"Server=(localdb)\MSSQLLocalDB;Database=POS_Dev;Trusted_Connection=True;TrustServerCertificate=True;";

        optionsBuilder.UseSqlServer(connectionString);

        return new POSDbContext(optionsBuilder.Options);
    }
}
