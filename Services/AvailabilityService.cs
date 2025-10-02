using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Services
{
    /// <summary>
    /// Global çalışma saatleri / blackout / min notice kontrolü +
    /// veritabanındaki randevulara göre slot çakışma kontrolü.
    /// </summary>
    public sealed class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _db;
        public AvailabilityService(AppDbContext db) => _db = db;

        // BusinessSettings.SlotMinutes boş/0 ise kullanılacak varsayılan
        private const int DefaultSlotMinutes = 45;

        /// <summary>
        /// Açılış saatleri, blackout günleri ve min notice kuralları açısından slot uygun mu?
        /// </summary>
        public async Task<bool> IsSlotBookable(DateTimeOffset startUtc)
        {
            var settings = await _db.BusinessSettings
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? new BusinessSettings();

            // Timezone (Windows/Linux uyumlu)
            TimeZoneInfo tz;
            try
            {
                var tzId = string.IsNullOrWhiteSpace(settings.TimeZone) ? "Europe/Brussels" : settings.TimeZone;
                tz = TZConvert.GetTimeZoneInfo(tzId);
            }
            catch { tz = TimeZoneInfo.Utc; }

            var localStart = TimeZoneInfo.ConvertTime(startUtc, tz).DateTime;

            var date = DateOnly.FromDateTime(localStart);
            var time = TimeOnly.FromDateTime(localStart);

            var slotMinutes = settings.SlotMinutes > 0 ? settings.SlotMinutes : DefaultSlotMinutes;
            var slotEnd = time.AddMinutes(slotMinutes);

            var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz).DateTime;
            if (settings.MinNoticeHours > 0 && localStart < nowLocal.AddHours(settings.MinNoticeHours))
                return false;

            var isBlackout = await _db.BlackoutDates
                .AsNoTracking()
                .AnyAsync(b => b.Date == date);
            if (isBlackout) return false;

            var wh = await _db.WeeklyOpenHours
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Day == localStart.DayOfWeek);

            if (wh is null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return false;

            if (time < wh.Open || slotEnd > wh.Close)
                return false;

            return true;
        }

        /// <summary>
        /// Veritabanındaki randevulara (Confirmed) ve süresi dolmamış hold'lara (Held && HoldUntilUtc > now)
        /// göre bu slot boş mu? (stilist bazında)
        /// </summary>
        public async Task<bool> IsSlotFreeAsync(
            int stylistId,
            DateTimeOffset startUtc,
            int durationMinutes,
            CancellationToken ct = default)
        {
            var endUtc = startUtc.AddMinutes(durationMinutes);

            // Overlap kuralı: (a.StartUtc < endUtc) AND (a.EndUtc > startUtc)
            var anyOverlap = await _db.Appointments.AsNoTracking()
                .Where(a => a.StylistId == stylistId)
                .Where(a =>
                    a.Status == AppointmentStatus.Confirmed ||
                    (a.Status == AppointmentStatus.Held && a.HoldUntilUtc > DateTimeOffset.UtcNow))
                .AnyAsync(a => a.StartUtc < endUtc && a.EndUtc > startUtc, ct);

            return !anyOverlap;
        }
    }
}
