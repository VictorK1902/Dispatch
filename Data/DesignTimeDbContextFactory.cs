using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dispatch.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DispatchDbContext>
{
    public DispatchDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DispatchConnection")
            ?? throw new InvalidOperationException(
                "Set the `DispatchConnection` environment variable to the Azure SQL connection string.");

        var options = new DbContextOptionsBuilder<DispatchDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new DispatchDbContext(options);
    }
}
