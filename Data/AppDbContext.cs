using Microsoft.EntityFrameworkCore;
using LogisticsAPI.Models;
namespace LogisticsAPI.Data;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<User> Users => Set<User>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Cargo> Cargoes => Set<Cargo>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripStatusHistory> TripStatusHistories => Set<TripStatusHistory>();
    public DbSet<FuelRequest> FuelRequests => Set<FuelRequest>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<Trip>().HasOne(t => t.Driver).WithMany(u => u.Trips)
            .HasForeignKey(t => t.DriverId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Trip>().HasOne(t => t.Vehicle).WithMany(v => v.Trips)
            .HasForeignKey(t => t.VehicleId).OnDelete(DeleteBehavior.SetNull);
        mb.Entity<Cargo>().HasOne(c => c.Trip).WithMany(t => t.CargoItems)
            .HasForeignKey(c => c.TripId).OnDelete(DeleteBehavior.SetNull);
    }
}
