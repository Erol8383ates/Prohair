using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using ProHair.NL.Data;
using ProHair.NL.Models;

namespace ProHair.NL.Services
{
    public sealed class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan SettingsCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan WeeklyHoursCacheTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan BlackoutsCacheTtl  = TimeSpan.FromMinutes(5);

        public AvailabilityService(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }

        public async Task<bool> IsSlotBookable(DateTimeOffset startUtc)
        {
            var settings = await _cache.GetOrCreateAsync("BusinessSettings", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = SettingsCacheTtl;
                return await _db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync() ?? new BusinessSettings();
            });

            // Safe TZ fallback
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone ?? "UTC"); }
            catch { tz = TimeZoneInfo.Utc; }

            var localDateTime = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;
            var dateOnly = DateOnly.FromDateTime(localDateTime);
            var timeOnly = TimeOnly.FromDateTime(localDateTime);

            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (settings.MinNoticeHours > 0 && localDateTime < nowLocal.AddHours(settings.MinNoticeHours))
                return false;

            var blackoutsKey = $"Blackouts:{dateOnly.Year}-{dateOnly.Month}";
            var monthBlackouts = await _cache.GetOrCreateAsync(blackoutsKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = BlackoutsCacheTtl;
                return (await _db.BlackoutDates.AsNoTracking()
                        .Where(b => b.Date.Year == dateOnly.Year && b.Date.Month == dateOnly.Month)
                        .Select(b => b.Date)
                        .ToListAsync())
                    .ToHashSet();
            });

            if (monthBlackouts.Contains(dateOnly))
                return false;

            var weeklyKey = "WeeklyOpenHours:Dict";
            var weeklyDict = await _cache.GetOrCreateAsync(weeklyKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = WeeklyHoursCacheTtl;
                var list = await _db.WeeklyOpenHours.AsNoTracking().ToListAsync();
                return list.ToDictionary(x => (DayOfWeek)x.Day, x => x);
            });

            if (!weeklyDict.TryGetValue(localDateTime.DayOfWeek, out var wh) || wh is null)
                return false;

            if (wh.IsClosed || wh.Open is null || wh.Close is null)
                return false;

            var slotEnd = timeOnly.AddMinutes(settings.SlotMinutes);
            if (timeOnly < wh.Open || slotEnd > wh.Close)
                return false;

            return true;
        }
    }
}
