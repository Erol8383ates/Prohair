using System.Threading.Tasks;
using System;
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
            var settings = await _db.BusinessSettings.FirstOrDefaultAsync()
                           ?? new BusinessSettings();

            var tz = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone);

            var local = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;
            var date = DateOnly.FromDateTime(local);
            var time = TimeOnly.FromDateTime(local);
            var slotEnd = time.AddMinutes(settings.SlotMinutes);

            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (local < nowLocal.AddHours(settings.MinNoticeHours)) return false;

            if (await _db.BlackoutDates.AnyAsync(b => b.Date == date)) return false;

            var wh = await _db.WeeklyOpenHours.SingleAsync(x => x.Day == local.DayOfWeek);
            if (wh.IsClosed || wh.Open is null || wh.Close is null) return false;
            if (time < wh.Open || slotEnd > wh.Close) return false;

            return true;
        }
    }
}
