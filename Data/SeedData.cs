using Microsoft.EntityFrameworkCore;
using ProHair.NL.Models;

namespace ProHair.NL.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeededAsync(AppDbContext db)
        {
            // --- Services (name + active only; minutes/price default to 0 via overload) ---
            await EnsureService(db, "Knippen", true);
            await EnsureService(db, "Föhnen", true);
            await EnsureService(db, "Kleuren", true);

            // Hair prothese services (no minutes shown)
            await EnsureService(db, "Consult – haarprothese", true);
            await EnsureService(db, "Plaatsing/maatwerk haarprothese", true);
            await EnsureService(db, "Onderhoud / re-bonding", true);

            // --- Hidden technical resource so BookingService works, but never shown in UI ---
            var studio = await db.Stylists.FirstOrDefaultAsync(s => s.Name == "Studio");
            if (studio == null)
            {
                studio = new Stylist { Name = "Studio", IsActive = true };
                db.Stylists.Add(studio);
                await db.SaveChangesAsync(); // get Id
            }
            else if (!studio.IsActive)
            {
                studio.IsActive = true;
            }

            // Default hours Mon–Sat 09:00–18:00 (add only missing)
            for (int d = 1; d <= 6; d++)
            {
                bool exists = await db.WorkingHours
                    .AnyAsync(w => w.StylistId == studio.Id && w.DayOfWeek == d);
                if (!exists)
                {
                    db.WorkingHours.Add(new WorkingHour
                    {
                        StylistId = studio.Id,
                        DayOfWeek = d,
                        StartLocal = new TimeSpan(9, 0, 0),
                        EndLocal = new TimeSpan(18, 0, 0)
                    });
                }
            }

            await db.SaveChangesAsync();
        }

        // Overload: name + isActive only (defaults minutes/price to 0)
        private static Task<Service> EnsureService(AppDbContext db, string name, bool isActive)
            => EnsureService(db, name, 0, 0m, isActive);

        // Full helper (kept for flexibility)
        private static async Task<Service> EnsureService(
            AppDbContext db, string name, int durationMinutes, decimal price, bool isActive)
        {
            var svc = await db.Services.FirstOrDefaultAsync(s => s.Name == name);
            if (svc == null)
            {
                svc = new Service
                {
                    Name = name,
                    DurationMinutes = durationMinutes,
                    Price = price,
                    IsActive = isActive
                };
                db.Services.Add(svc);
            }
            else
            {
                svc.DurationMinutes = durationMinutes;
                svc.Price = price;
                svc.IsActive = isActive;
            }
            return svc;
        }
    }
}
