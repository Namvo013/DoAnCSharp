using LiMS.Api.Models.Legacy;
using Microsoft.EntityFrameworkCore;

namespace LiMS.Api.Data;

public sealed class LegacyLiMSDbContext(DbContextOptions<LegacyLiMSDbContext> options) : DbContext(options)
{
    public DbSet<LegacyUser> Users => Set<LegacyUser>();
    public DbSet<LegacyBook> Books => Set<LegacyBook>();
    public DbSet<LegacyLoan> Loans => Set<LegacyLoan>();
    public DbSet<LegacyReservation> Reservations => Set<LegacyReservation>();
    public DbSet<LegacyNotification> Notifications => Set<LegacyNotification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LegacyUser>().ToTable("Users");
        modelBuilder.Entity<LegacyBook>().ToTable("Books");
        modelBuilder.Entity<LegacyLoan>().ToTable("Loans");
        modelBuilder.Entity<LegacyReservation>().ToTable("Reservations");
        modelBuilder.Entity<LegacyNotification>().ToTable("Notifications");
    }
}
