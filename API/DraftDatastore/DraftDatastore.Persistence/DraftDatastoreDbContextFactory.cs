using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraftDatastore.Persistence;

public sealed class DraftDatastoreDbContextFactory : IDesignTimeDbContextFactory<DraftDatastoreDbContext>
{
    public DraftDatastoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DRAFT_DATASTORE_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Set DRAFT_DATASTORE_CONNECTION_STRING before creating or applying EF Core migrations.");
        }

        var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(DraftDatastoreDbContext).Assembly.FullName))
            .Options;
        return new DraftDatastoreDbContext(options);
    }
}
