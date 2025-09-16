using System;
using System.Collections.Generic;
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

        // Cache süreleri
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
            // 1) Settings (cache)
            var settings = await _cache.GetOrCreateAsync("BusinessSettings", async e =>
            {
                e.AbsoluteExpirationRelativeToNow = SettingsCacheTtl;
                return await _db.BusinessSettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync() ?? new BusinessSettings();
            });

            // Time zone bul (settings.TimeZone boş/yanlışsa UTC fallback)
            TimeZoneInfo tz;
            try { tz = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone ?? "UTC"); }
            catch { tz = TimeZoneInfo.Utc; }

            // 2) Yerel zamanlar
            var localDateTime = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;
            var dateOnly = DateOnly.FromDateTime(localDateTime);
            var timeOnly = TimeOnly.FromDateTime(localDateTime);

            // Min notice kontrolü (yerelde şimdi + MinNoticeHours)
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (settings.MinNoticeHours > 0 && localDateTime < nowLocal.AddHours(settings.MinNoticeHours))
                return false;

            // 3) Blackout (cache: yıl-ay)
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

            // 4) Weekly open hours (cache: tüm hafta sözlüğü)
            var weeklyKey = "WeeklyOpenHours:Dict";
            var weeklyDict = await _cache.GetOrCreateAsync(weeklyKey, async e =>
            {
                e.AbsoluteExpirationRelativeToNow = WeeklyHoursCacheTtl;
                var list = await _db.WeeklyOpenHours.AsNoTracking().ToListAsync();
                // DB'deki Day türü ile System.DayOfWeek aynı ise direkt cast çalışır.
                return list.ToDictionary(x => (DayOfWeek)x.Day, x => x);
            });

            if (!weeklyDict.TryGetValue(localDateTime.DayOfWeek, out var wh) || wh is null)
                return false;

            // Kapalı gün veya saatler boş ise
            if (wh.IsClosed || wh.Open is null || wh.Close is null)
                return false;

            // Slot uç zamanı (hizmet süresi burada bilinmiyor; servis bu metodu slot başlangıcını doğrulamak için çağırıyor)
            // Yine de slot genişliğini kontrol ederek "mesai dışı"nın taşmamasını sağlarız.
            var slotEnd = timeOnly.AddMinutes(settings.SlotMinutes);

            if (timeOnly < wh.Open || slotEnd > wh.Close)
                return false;

            // Tüm kontroller geçti
            return true;
        }
    }
}
