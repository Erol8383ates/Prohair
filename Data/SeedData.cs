using Microsoft.EntityFrameworkCore;
using ProHair.NL.Models;

namespace ProHair.NL.Data
{
    public static class SeedData
    {
        public static async Task EnsureSeededAsync(AppDbContext db)
        {
            if (!await db.Services.AnyAsync())
            {
                db.Services.AddRange(
                    new Service { Name = "Knippen", DurationMinutes = 30, Price = 25 },
                    new Service { Name = "Kleuren", DurationMinutes = 60, Price = 60 },
                    new Service { Name = "Föhnen", DurationMinutes = 30, Price = 20 }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.Stylists.AnyAsync())
            {
                var anna = new Stylist { Name = "Anna" };
                var marco = new Stylist { Name = "Marco" };
                db.Stylists.AddRange(anna, marco);
                await db.SaveChangesAsync();

                // Mon–Sat 09:00–18:00
                foreach (var s in new[] { anna, marco })
                {
                    for (int d = 1; d <= 6; d++)
                    {
                        db.WorkingHours.Add(new WorkingHour
                        {
                            StylistId = s.Id,
                            DayOfWeek = d,
                            StartLocal = new TimeSpan(9, 0, 0),
                            EndLocal = new TimeSpan(18, 0, 0)
                        });
                    }
                }
                await db.SaveChangesAsync();
            }
        }
    }
}
