using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;
using ProHair.NL.Services;

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

        // === CONFIG (unchanged on client; just serves Services & Stylists) ===
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var services = await _db.Services
                .Select(s => new { id = s.Id, name = s.Name, durationMinutes = s.DurationMinutes })
                .ToListAsync();

            var stylists = await _db.Stylists
                .Select(s => new { id = s.Id, name = s.Name })
                .ToListAsync();

            return Ok(new { services, stylists });
        }

        // === SLOT GENERATION (now honors WeeklyOpenHours + BlackoutDates) ===
        // client calls: /api/availability?date=YYYY-MM-DD&stylistId=1&serviceId=2&tz=Europe%2FBrussels
        [HttpGet]
        public async Task<IActionResult> GetSlots(
            [FromQuery] string date,
            [FromQuery] int stylistId,
            [FromQuery] int serviceId,
            [FromQuery] string? tz = null)
        {
            // Load settings
            var settings = await _db.BusinessSettings.FirstOrDefaultAsync()
                           ?? new BusinessSettings();

            // Parse date safely
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return Ok(Array.Empty<string>());

            // Determine timezone (URL param overrides settings if provided)
            var tzid = string.IsNullOrWhiteSpace(tz) ? settings.TimeZone : tz;
            TimeZoneInfo zone;
            try { zone = TimeZoneInfo.FindSystemTimeZoneById(tzid); }
            catch { zone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZone); }

            // Weekly hours + blackouts
            var wh = await _db.WeeklyOpenHours.SingleOrDefaultAsync(x => x.Day == d.DayOfWeek);
            if (wh == null || wh.IsClosed || wh.Open is null || wh.Close is null)
                return Ok(Array.Empty<string>());

            if (await _db.BlackoutDates.AnyAsync(b => b.Date == d))
                return Ok(Array.Empty<string>());

            // Service duration; fall back to slot length
            var svc = await _db.Services.FindAsync(serviceId);
            var durationMin = svc?.DurationMinutes > 0 ? svc!.DurationMinutes : settings.SlotMinutes;
            var stepMin = settings.SlotMinutes;

            // Build local open/close DateTimes
            var openLocal = d.ToDateTime(wh.Open.Value);
            var closeLocal = d.ToDateTime(wh.Close.Value);

            // Generate slots in local time, then ask the availability service (server-side rules)
            var slots = new List<string>();
            for (var t = openLocal; t.AddMinutes(durationMin) <= closeLocal; t = t.AddMinutes(stepMin))
            {
                // Convert to UTC for server checks
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(t, zone);
                var ok = await _availability.IsSlotBookable(new DateTimeOffset(startUtc, TimeSpan.Zero));
                if (!ok) continue;

                // Return ISO **local** times (client expects local ISO, no timezone suffix)
                // Example: "2025-09-04T10:00:00"
                slots.Add(t.ToString("yyyy-MM-dd'T'HH:mm:ss"));
            }

            return Ok(slots);
        }
    }
}
