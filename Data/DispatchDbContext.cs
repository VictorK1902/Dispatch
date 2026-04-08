using Dispatch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Data;

public class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<JobModule> JobModules => Set<JobModule>();
    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasOne(e => e.JobModule)
                .WithMany()
                .HasForeignKey(e => e.JobModuleId);
        });

        modelBuilder.Entity<JobModule>().HasData(
            new JobModule
            {
                Id = 1,
                Name = "Weather Report",
                Description = "Return a weather report in terms of temperature",
                CreatedAt = new DateTimeOffset(2026, 4, 6, 0, 0, 0, TimeSpan.FromHours(-5))
            },
            new JobModule
            {
                Id = 2,
                Name = "Stock Price Report",
                Description = "Return a full monthly historical price of a stock symbol",
                CreatedAt = new DateTimeOffset(2026, 4, 6, 0, 0, 0, TimeSpan.FromHours(-5))
            }
        );
    }
}
