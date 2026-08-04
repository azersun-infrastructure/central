using Microsoft.EntityFrameworkCore;
using OpsCentral.Models.Entities;

namespace OpsCentral.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AdActionRequest> AdActionRequests => Set<AdActionRequest>();
    public DbSet<AdActionEvent> AdActionEvents => Set<AdActionEvent>();
    public DbSet<LocalAdminAccount> LocalAdminAccounts => Set<LocalAdminAccount>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdActionRequest>(entity =>
        {
            entity.HasIndex(e => e.RequestedAtUtc);
            entity.HasIndex(e => e.Status);

            entity.HasMany(e => e.Events)
                .WithOne(e => e.AdActionRequest)
                .HasForeignKey(e => e.AdActionRequestId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LocalAdminAccount>(entity =>
        {
            entity.HasIndex(e => e.Username).IsUnique();
        });
    }
}
