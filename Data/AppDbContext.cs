using Microsoft.EntityFrameworkCore;
using ProHair.NL.Models;

namespace ProHair.NL.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Service> Services => Set<Service>();
        public DbSet<Stylist> Stylists => Set<Stylist>();
        public DbSet<WorkingHour> WorkingHours => Set<WorkingHour>();
        public DbSet<TimeOff> TimeOffs => Set<TimeOff>();
        public DbSet<Appointment> Appointments => Set<Appointment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // decimal -> numeric(10,2)
            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasPrecision(10, 2);

            // Prevent double booking (Postgres syntax: use double quotes, not [ ])
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.StylistId, a.StartUtc })
                .IsUnique()
                .HasFilter("\"Status\" IN (0,1)"); // 0 = Hold, 1 = Confirmed (adjust if different)
        }
    }
}
