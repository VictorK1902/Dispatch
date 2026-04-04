using Dispatch.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Data;

public class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<JobModule> JobModules => Set<JobModule>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.HasOne(e => e.JobModule)
                .WithMany()
                .HasForeignKey(e => e.JobModuleId);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasOne(e => e.Job)
                .WithMany()
                .HasForeignKey(e => e.JobId);
        });
    }
}
