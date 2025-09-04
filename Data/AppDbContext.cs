using System;
using System.Linq;
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

        // NEW
        public DbSet<BusinessSettings> BusinessSettings => Set<BusinessSettings>();
        public DbSet<WeeklyOpenHours> WeeklyOpenHours => Set<WeeklyOpenHours>();
        public DbSet<BlackoutDate> BlackoutDates => Set<BlackoutDate>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Price precision
            modelBuilder.Entity<Service>()
                .Property(s => s.Price)
                .HasPrecision(10, 2);

            // Prevent double booking (Postgres filter syntax)
            modelBuilder.Entity<Appointment>()
                .HasIndex(a => new { a.StylistId, a.StartUtc })
                .IsUnique()
                .HasFilter("\"Status\" IN (0,1)"); // 0 = Hold, 1 = Confirmed

            // Seed settings
            modelBuilder.Entity<BusinessSettings>().HasData(
                new BusinessSettings
                {
                    Id = 1,
                    TimeZone = "Europe/Brussels",
                    SlotMinutes = 30,
                    MinNoticeHours = 2,
                    MaxSimultaneousBookings = 1
                }
            );

            // Seed weekly hours (Mon–Sat 10:00–19:00, Sun closed)
            var weeklySeed = Enum.GetValues<DayOfWeek>().Select((d, i) =>
                new WeeklyOpenHours
                {
                    Id = i + 1,
                    Day = d,
                    IsClosed = (d == DayOfWeek.Sunday),
                    Open = new TimeOnly(10, 0),
                    Close = new TimeOnly(19, 0)
                });
            modelBuilder.Entity<WeeklyOpenHours>().HasData(weeklySeed);
        }
    }
}
