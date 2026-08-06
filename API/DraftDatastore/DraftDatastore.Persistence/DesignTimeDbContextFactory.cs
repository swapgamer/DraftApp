using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DraftDatastore.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DraftDatastoreDbContext>
{
    public DraftDatastoreDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DRAFT_DATASTORE_CONNECTION_STRING")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=DraftDatastore;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<DraftDatastoreDbContext>().UseSqlServer(connectionString).Options;
        return new DraftDatastoreDbContext(options);
    }
}
