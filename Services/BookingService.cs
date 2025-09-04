using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Services
{
    public class BookingService
    {
        private readonly AppDbContext _db;

        // SLOT SPACING: set to 45 to show times every 45 minutes
        private const int SlotStepMinutes = 45;

        // Small buffer after each service (minutes)
        private const int BufferMinutes = 5;

        public BookingService(AppDbContext db) { _db = db; }

        private static TimeZoneInfo GetTz(string tzId)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { return TZConvert.GetTimeZoneInfo(tzId); }
        }

        public async Task<List<DateTime>> GetAvailableSlotsLocalAsync(
            DateTime dateLocal, int stylistId, int serviceId, string tzId = "Europe/Brussels")
        {
            var tz = GetTz(tzId);
            var service = await _db.Services.FirstAsync(s => s.Id == serviceId && s.IsActive);
            var stylist = await _db.Stylists
                .Include(s => s.WorkingHours)
                .Include(s => s.TimeOffs)
                .FirstAsync(s => s.Id == stylistId && s.IsActive);

            var dow = (int)dateLocal.DayOfWeek;
            var dayHours = stylist.WorkingHours.Where(w => w.DayOfWeek == dow).ToList();
            if (!dayHours.Any()) return new List<DateTime>();

            var dayStartLocal = dateLocal.Date;
            var dayEndLocal = dayStartLocal.AddDays(1);
            var dayStartUtc = TimeZoneInfo.ConvertTimeToUtc(dayStartLocal, tz);
            var dayEndUtc = TimeZoneInfo.ConvertTimeToUtc(dayEndLocal, tz);
            var nowLocal = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);

            var activeAppointments = await _db.Appointments
                .Where(a => a.StylistId == stylistId
                            && a.Status != AppointmentStatus.Cancelled
                            && a.EndUtc > dayStartUtc && a.StartUtc < dayEndUtc)
                .Include(a => a.Service)
                .ToListAsync();

            activeAppointments = activeAppointments
                .Where(a => a.Status == AppointmentStatus.Confirmed ||
                            (a.Status == AppointmentStatus.Hold && a.HoldUntilUtc > DateTime.UtcNow))
                .ToList();

            var busyLocal = activeAppointments.Select(a => new
            {
                Start = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, tz),
                End = TimeZoneInfo.ConvertTimeFromUtc(a.EndUtc, tz)
            }).ToList();

            var timeOffs = stylist.TimeOffs
                .Where(t => t.EndLocal > dayStartLocal && t.StartLocal < dayEndLocal)
                .ToList();

            var duration = TimeSpan.FromMinutes(service.DurationMinutes + BufferMinutes);
            var step = TimeSpan.FromMinutes(SlotStepMinutes);
            var slots = new List<DateTime>();

            foreach (var wh in dayHours)
            {
                var whStartLocal = dayStartLocal.Add(wh.StartLocal);
                var whEndLocal = dayStartLocal.Add(wh.EndLocal);

                for (var t = whStartLocal; t + duration <= whEndLocal; t = t.Add(step))
                {
                    if (dateLocal.Date == nowLocal.Date && t < nowLocal.AddMinutes(15)) continue;

                    var slotEnd = t + duration;
                    bool overlapsTimeOff = timeOffs.Any(to => t < to.EndLocal && slotEnd > to.StartLocal);
                    if (overlapsTimeOff) continue;

                    bool overlapsBusy = busyLocal.Any(b => t < b.End && slotEnd > b.Start);
                    if (overlapsBusy) continue;

                    slots.Add(t);
                }
            }

            return slots.Distinct().OrderBy(x => x).ToList();
        }

        public async Task<(bool ok, string? err, Appointment? hold)> CreateHoldAsync(
            int stylistId, int serviceId, DateTime startLocal, string tzId = "Europe/Brussels", int holdMinutes = 10)
        {
            var tz = GetTz(tzId);
            var service = await _db.Services.FirstAsync(s => s.Id == serviceId && s.IsActive);
            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = startUtc.AddMinutes(service.DurationMinutes + BufferMinutes);

            using var trx = await _db.Database.BeginTransactionAsync();
            try
            {
                var expired = _db.Appointments.Where(a => a.Status == AppointmentStatus.Hold && a.HoldUntilUtc < DateTime.UtcNow);
                _db.Appointments.RemoveRange(expired);
                await _db.SaveChangesAsync();

                bool conflict = await _db.Appointments.AnyAsync(a => a.StylistId == stylistId
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

        // ---- Admin cancel helpers ----
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
