using LogisticsAPI.Models;
using Microsoft.EntityFrameworkCore;
namespace LogisticsAPI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Cargo> Cargoes => Set<Cargo>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripStatusHistory> TripStatusHistories => Set<TripStatusHistory>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SmtpSetting> SmtpSettings => Set<SmtpSetting>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Company>(e =>
        {
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.CreatedAt);
        });

        mb.Entity<User>(e =>
        {
            e.HasIndex(x => x.Username).IsUnique();
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.InviteToken).IsUnique().HasFilter("[InviteToken] IS NOT NULL");
            e.HasOne(x => x.Company).WithMany(c => c.Users)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Vehicle>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.Capacity).HasPrecision(18, 2);
            e.HasOne(x => x.Company).WithMany(c => c.Vehicles)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Cargo>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.Property(x => x.WeightTons).HasPrecision(18, 2);
            e.Property(x => x.VolumeCbm).HasPrecision(18, 2);
            e.HasOne(x => x.Company).WithMany(c => c.Cargoes)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Trip).WithMany(t => t.CargoItems)
                .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<Trip>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.DistanceKm).HasPrecision(18, 2);
            e.HasOne(x => x.Company).WithMany(c => c.Trips)
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Driver).WithMany(u => u.Trips)
                .HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Vehicle).WithMany(v => v.Trips)
                .HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<TripStatusHistory>(e =>
        {
            e.HasOne(x => x.Trip).WithMany(t => t.StatusHistory)
                .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.Cascade);
        });

        mb.Entity<Message>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.HasIndex(x => new { x.CompanyId, x.DriverId, x.ManagerId });
            e.HasOne(x => x.Company).WithMany()
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Driver).WithMany()
                .HasForeignKey(x => x.DriverId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Manager).WithMany()
                .HasForeignKey(x => x.ManagerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Sender).WithMany()
                .HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<Incident>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.HasOne(x => x.Company).WithMany()
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Trip).WithMany()
                .HasForeignKey(x => x.TripId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Reporter).WithMany()
                .HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        mb.Entity<SupportTicket>(e =>
        {
            e.HasIndex(x => x.CompanyId);
            e.HasOne(x => x.Company).WithMany()
                .HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedTo).WithMany()
                .HasForeignKey(x => x.AssignedToUserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
