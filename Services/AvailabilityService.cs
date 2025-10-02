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
    public sealed class AvailabilityService : IAvailabilityService
    {
        private readonly AppDbContext _db;
        public AvailabilityService(AppDbContext db) => _db = db;

        private const int DefaultSlotMinutes = 45;

        public async Task<bool> IsSlotBookable(DateTimeOffset startUtc)
        {
            var settings = await _db.BusinessSettings
                .AsNoTracking()
                .FirstOrDefaultAsync() ?? new BusinessSettings();

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

            var isBlackout = await _db.BlackoutDates.AsNoTracking()
                .AnyAsync(b => b.Date == date);
            if (isBlackout) return false;

            var wh = await _db.WeeklyOpenHours.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Day == localStart.DayOfWeek);

            if (wh is null || wh.IsClosed || wh.Open is null || wh.Close is null) return false;
            if (time < wh.Open || slotEnd > wh.Close) return false;

            return true;
        }

        public async Task<bool> IsSlotFreeAsync(
            int stylistId,
            DateTimeOffset startUtc,
            int durationMinutes,
            CancellationToken ct = default)
        {
            var endUtc = startUtc.AddMinutes(durationMinutes);

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
