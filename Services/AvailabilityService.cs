using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;

namespace ProHair.NL.Services
{
    public sealed class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _db;
        public AvailabilityService(AppDbContext db) => _db = db;

        public async Task<bool> IsSlotBookable(DateTimeOffset startUtc)
        {
            var settings = await _db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync()
                           ?? new BusinessSettings();

            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone ?? "UTC"); }
            catch { tz = TimeZoneInfo.Utc; }

            var local = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;
            var date = DateOnly.FromDateTime(local);
            var time = TimeOnly.FromDateTime(local);
            var slotEnd = time.AddMinutes(settings.SlotMinutes);

            // Min notice
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (settings.MinNoticeHours > 0 && local < nowLocal.AddHours(settings.MinNoticeHours))
                return false;

            // Blackout (tam gün kapalı)
            if (await _db.BlackoutDates.AsNoTracking().AnyAsync(b => b.Date == date))
                return false;

            // Weekly open hours (gün kapalı veya saat dışında ise iptal)
            var wh = await _db.WeeklyOpenHours
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Day == local.DayOfWeek);

            if (wh is null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return false;

            if (time < wh.Open || slotEnd > wh.Close)
                return false;

            return true;
        }
    }
}
