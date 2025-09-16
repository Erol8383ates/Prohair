using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Services
{
    /// <summary>
    /// Tek bir slot başlangıcının, global çalışma saatleri ve blackout günlerine göre
    /// rezervasyona uygun olup olmadığını kontrol eder.
    /// Not: Burada stilist/time-off veya servis süresi denetimi yoktur;
    /// bunlar slot üretimini yapan BookingService tarafında ele alınır.
    /// </summary>
    public sealed class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _db;
        public AvailabilityService(AppDbContext db) => _db = db;

        // BusinessSettings.SlotMinutes boş/0 ise kullanılacak varsayılan
        private const int DefaultSlotMinutes = 45;

        public async Task<bool> IsSlotBookable(DateTimeOffset startUtc)
        {
            // Global ayarlar
            var settings = await _db.BusinessSettings
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? new BusinessSettings();

            // Timezone (hem Windows hem Linux uyumlu)
            var tzId = settings.TimeZone;
            TimeZoneInfo tz;
            try
            {
                tz = !string.IsNullOrWhiteSpace(tzId)
                    ? TZConvert.GetTimeZoneInfo(tzId)
                    : TZConvert.GetTimeZoneInfo("Europe/Brussels");
            }
            catch
            {
                tz = TimeZoneInfo.Utc;
            }

            // UTC -> local
            var localStart = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;

            // Tarih/saat parçaları
            var date = DateOnly.FromDateTime(localStart);
            var time = TimeOnly.FromDateTime(localStart);

            // Slot uzunluğu: global slot minutes (servis süresi değil)
            var slotMinutes = settings.SlotMinutes > 0 ? settings.SlotMinutes : DefaultSlotMinutes;
            var slotEnd = time.AddMinutes(slotMinutes);

            // Minimum haber süresi (MinNoticeHours)
            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (settings.MinNoticeHours > 0 && localStart < nowLocal.AddHours(settings.MinNoticeHours))
                return false;

            // Blackout: tam gün kapalıysa
            var isBlackout = await _db.BlackoutDates
                .AsNoTracking()
                .AnyAsync(b => b.Date == date);
            if (isBlackout) return false;

            // Haftalık çalışma saatleri: gün kapalı veya saatler boş ise
            var wh = await _db.WeeklyOpenHours
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Day == localStart.DayOfWeek);

            if (wh is null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return false;

            // Saat aralığı kontrolü (başlangıç içeride olmalı ve slot bitişi kapanışı aşmamalı)
            if (time < wh.Open || slotEnd > wh.Close)
                return false;

            return true;
        }
    }
}
