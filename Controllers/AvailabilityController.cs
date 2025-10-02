using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using ProHair.NL.Services;
using TimeZoneConverter;

namespace ProHair.NL.Controllers
{
    [ApiController]
    [Route("api/availability")]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IAvailabilityService _availability;

        public AvailabilityController(AppDbContext db, IAvailabilityService availability)
        {
            _db = db;
            _availability = availability;
        }

        // CONFIG: Services & Stylists
        [HttpGet("config")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetConfig()
        {
            var services = await _db.Services
                .AsNoTracking()
                .Select(s => new { id = s.Id, name = s.Name, durationMinutes = s.DurationMinutes })
                .ToListAsync();

            var stylists = await _db.Stylists
                .AsNoTracking()
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { services, stylists });
        }

        // SLOTS
        // GET /api/availability?date=YYYY-MM-DD&stylistId=1&serviceId=2&tz=Europe%2FBrussels
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetSlots(
            [FromQuery] string date,
            [FromQuery] int stylistId,
            [FromQuery] int serviceId,
            [FromQuery] string? tz = null)
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return Ok(Array.Empty<string>());

            var settings = await _db.BusinessSettings.AsNoTracking().FirstOrDefaultAsync()
                           ?? new BusinessSettings();

            // Safe timezone resolution (works on Windows/Linux)
            TimeZoneInfo zone;
            try
            {
                var tzId = !string.IsNullOrWhiteSpace(tz) ? tz : settings.TimeZone;
                zone = !string.IsNullOrWhiteSpace(tzId)
                    ? TZConvert.GetTimeZoneInfo(tzId)
                    : TZConvert.GetTimeZoneInfo("Europe/Brussels");
            }
            catch { zone = TimeZoneInfo.Utc; }

            var wh = await _db.WeeklyOpenHours
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Day == d.DayOfWeek);

            if (wh == null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return Ok(Array.Empty<string>());

            var isBlackout = await _db.BlackoutDates
                .AsNoTracking()
                .AnyAsync(b => b.Date == d);

            if (isBlackout)
                return Ok(Array.Empty<string>());

            var svc = await _db.Services.AsNoTracking().FirstOrDefaultAsync(x => x.Id == serviceId);
            var stepMin = settings.SlotMinutes > 0 ? settings.SlotMinutes : 45;
            var durationMin = (svc?.DurationMinutes ?? 0) > 0 ? svc!.DurationMinutes : stepMin;

            var openLocal = d.ToDateTime(wh.Open.Value);
            var closeLocal = d.ToDateTime(wh.Close.Value);

            var slots = new List<string>();
            for (var t = openLocal; t.AddMinutes(durationMin) <= closeLocal; t = t.AddMinutes(stepMin))
            {
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(t, zone);
                var ok = await _availability.IsSlotBookable(new DateTimeOffset(startUtc, TimeSpan.Zero));
                if (!ok) continue;

                // stylist overlap check (service duration)
                var free = await _availability.IsSlotFreeAsync(stylistId, new DateTimeOffset(startUtc, TimeSpan.Zero), durationMin);
                if (!free) continue;

                slots.Add(t.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
            }

            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";

            return Ok(slots);
        }

        // Quick check for a single slot
        // GET /api/availability/slot-free?stylistId=1&start=2025-10-02T12:00:00Z&duration=45
        [HttpGet("slot-free")]
        public async Task<IActionResult> IsSlotFree(
            [FromQuery] int stylistId,
            [FromQuery] DateTimeOffset start,
            [FromQuery] int duration = 45,
            CancellationToken ct = default)
        {
            var bookable = await _availability.IsSlotBookable(start);
            if (!bookable)
                return Ok(new { ok = false, reason = "Not bookable by business rules." });

            var free = await _availability.IsSlotFreeAsync(stylistId, start, duration, ct);
            return Ok(new { ok = free, reason = free ? null : "Overlaps with existing appointment/hold." });
        }
    }
}
