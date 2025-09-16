using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Services
{
    public class BookingService
    {
        private readonly AppDbContext _db;

        // Servis bittiğinde küçük tampon (dk)
        private const int BufferMinutes = 5;

        public BookingService(AppDbContext db) { _db = db; }

        private static TimeZoneInfo GetTz(string? tzId)
        {
            tzId ??= "Europe/Brussels";
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { return TZConvert.GetTimeZoneInfo(tzId); }
        }

        /// <summary>
        /// Slot üretimi SADECE Admin › Availability tablolarına göre:
        /// WeeklyOpenHours + BlackoutDates + BusinessSettings.
        /// </summary>
        public async Task<List<DateTime>> GetAvailableSlotsLocalAsync(
            DateTime dateLocal,
            int stylistId,
            int serviceId,
            string tzId = "Europe/Brussels")
        {
            var settings = await _db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync()
                           ?? new BusinessSettings { SlotMinutes = 30, MinNoticeHours = 0, TimeZone = tzId };

            var tz = GetTz(settings.TimeZone ?? tzId);

            var service = await _db.Services
                .AsNoTracking()
                .FirstAsync(s => s.Id == serviceId && s.IsActive);

            // Gün açık mı?
            var dow = dateLocal.DayOfWeek;
            var wh = await _db.WeeklyOpenHours
                .AsNoTracking()
                .SingleOrDefaultAsync(w => w.Day == dow);

            var dayOnly = DateOnly.FromDateTime(dateLocal.Date);
            var isBlackout = await _db.BlackoutDates.AsNoTracking().AnyAsync(b => b.Date == dayOnly);

            if (isBlackout || wh is null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return new List<DateTime>();

            // Açılış / kapanış (TimeOnly -> TimeSpan -> DateTime)
            var openLocal = dateLocal.Date.Add(wh.Open.Value.ToTimeSpan());
            var closeLocal = dateLocal.Date.Add(wh.Close.Value.ToTimeSpan());

            // MinNotice + bugünün geçmiş saatlerini gizle
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            var minStartLocal = nowLocal.AddHours(settings.MinNoticeHours);

            var firstStart = openLocal;
            if (dateLocal.Date == minStartLocal.Date)
                firstStart = (openLocal > minStartLocal) ? openLocal : minStartLocal;

            var step = TimeSpan.FromMinutes(Math.Max(5, settings.SlotMinutes));
            var duration = TimeSpan.FromMinutes(service.DurationMinutes + BufferMinutes);

            // Gün içindeki randevular (hold süresi bitmemiş + confirmed)
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dateLocal.Date, tz);
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dateLocal.Date.AddDays(1), tz);

            var appts = await _db.Appointments
                .Where(a => a.StylistId == stylistId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.EndUtc > dayStartUtc && a.StartUtc < dayEndUtc)
                .Include(a => a.Service)
                .ToListAsync();

            var activeAppts = appts
                .Where(a => a.Status == AppointmentStatus.Confirmed ||
                            (a.Status == AppointmentStatus.Hold && a.HoldUntilUtc > DateTime.UtcNow))
                .Select(a => new
                {
                    StartLocal = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, tz),
                    EndLocal = TimeZoneInfo.ConvertTimeFromUtc(a.EndUtc, tz)
                })
                .ToList();

            // Stylist TimeOff (modelinizde adı TimeOff)
            var stylist = await _db.Stylists
                .Include(s => s.TimeOffs)
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == stylistId && s.IsActive);

            var timeOffs = stylist?.TimeOffs?
                .Where(t => t.EndLocal > dateLocal.Date && t.StartLocal < dateLocal.Date.AddDays(1))
                .ToList() ?? new List<TimeOff>();

            // Slot üretimi
            var slots = new List<DateTime>();
            for (var t = firstStart; t + duration <= closeLocal; t = t.Add(step))
            {
                var end = t + duration;

                if (timeOffs.Any(to => t < to.EndLocal && end > to.StartLocal))
                    continue;

                if (activeAppts.Any(b => t < b.EndLocal && end > b.StartLocal))
                    continue;

                slots.Add(t);
            }

            return slots.Distinct().OrderBy(x => x).ToList();
        }

        public async Task<(bool ok, string? err, Appointment? hold)> CreateHoldAsync(
            int stylistId, int serviceId, DateTime startLocal, string tzId = "Europe/Brussels", int holdMinutes = 10)
        {
            var tz = GetTz(tzId);
            var service = await _db.Services.FirstAsync(s => s.Id == serviceId && s.IsActive);

            // Bookable mı? (Weekly + Blackout + Notice)
            var settings = await _db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync()
                           ?? new BusinessSettings { SlotMinutes = 30, MinNoticeHours = 0, TimeZone = tzId };

            var date = startLocal.Date;
            var wh = await _db.WeeklyOpenHours.AsNoTracking().SingleOrDefaultAsync(w => w.Day == date.DayOfWeek);
            var isBlackout = await _db.BlackoutDates.AsNoTracking().AnyAsync(b => b.Date == DateOnly.FromDateTime(date));

            if (isBlackout || wh is null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return (false, "Deze dag is niet boekbaar.", null);

            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, GetTz(settings.TimeZone ?? tzId));
            if (startLocal < nowLocal.AddHours(settings.MinNoticeHours))
                return (false, "Te weinig tijd om te reserveren.", null);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = startUtc.AddMinutes(service.DurationMinutes + BufferMinutes);

            using var trx = await _db.Database.BeginTransactionAsync();
            try
            {
                var expired = _db.Appointments.Where(a => a.Status == AppointmentStatus.Hold && a.HoldUntilUtc < DateTime.UtcNow);
                _db.Appointments.RemoveRange(expired);
                await _db.SaveChangesAsync();

                var conflict = await _db.Appointments.AnyAsync(a => a.StylistId == stylistId
                    && a.Status != AppointmentStatus.Cancelled
                    && a.EndUtc > startUtc && a.StartUtc < endUtc);

                if (conflict) return (false, "Slot is net ingenomen. Kies een andere tijd.", null);

                var hold = new Appointment
                {
                    StylistId = stylistId,
                    ServiceId = serviceId,
                    StartUtc = startUtc,
                    EndUtc = endUtc,
                    Status = AppointmentStatus.Hold,
                    HoldUntilUtc = DateTime.UtcNow.AddMinutes(holdMinutes),
                    HoldToken = Guid.NewGuid().ToString("N")
                };

                _db.Appointments.Add(hold);
                await _db.SaveChangesAsync();
                await trx.CommitAsync();
                return (true, null, hold);
            }
            catch (DbUpdateException ex)
            {
                await trx.RollbackAsync();
                return (false, ex.Message, null);
            }
        }

        public async Task<(bool ok, string? err, Appointment? appt)> ConfirmAsync(
            string holdToken, string clientName, string clientEmail, string clientPhone)
        {
            var appt = await _db.Appointments
                .Include(a => a.Service)
                .FirstOrDefaultAsync(a => a.HoldToken == holdToken && a.Status == AppointmentStatus.Hold);

            if (appt == null) return (false, "Hold niet gevonden of verlopen.", null);
            if (appt.HoldUntilUtc <= DateTime.UtcNow) return (false, "Hold is verlopen.", null);

            appt.ClientName = clientName;
            appt.ClientEmail = clientEmail;
            appt.ClientPhone = clientPhone;
            appt.Status = AppointmentStatus.Confirmed;
            appt.HoldUntilUtc = null;
            appt.HoldToken = null;

            try { await _db.SaveChangesAsync(); return (true, null, appt); }
            catch (DbUpdateException ex) { return (false, ex.Message, null); }
        }

        public async Task<(bool ok, string? err)> ReleaseHoldAsync(string holdToken)
        {
            var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.HoldToken == holdToken && a.Status == AppointmentStatus.Hold);
            if (appt == null) return (false, null);
            _db.Appointments.Remove(appt);
            await _db.SaveChangesAsync();
            return (true, null);
        }

        public async Task<(bool ok, string? err, Appointment? appt)> CancelConfirmedAsync(int id)
        {
            var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id);
            if (appt == null) return (false, "Afspraak niet gevonden.", null);
            if (appt.Status == AppointmentStatus.Cancelled) return (true, null, appt);

            appt.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();
            return (true, null, appt);
        }

        public async Task<int> CancelManyAsync(IEnumerable<int> ids)
        {
            var set = await _db.Appointments.Where(a => ids.Contains(a.Id)).ToListAsync();
            foreach (var a in set) a.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();
            return set.Count;
        }
    }
}
