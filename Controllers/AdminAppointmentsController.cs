using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Hubs;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Controllers
{
    [ApiController]
    [Route("api/admin/appointments")]
    public class AdminAppointmentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<BookingHub> _hub;

        public AdminAppointmentsController(AppDbContext db, IHubContext<BookingHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private static TimeZoneInfo Brussels()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"); }
            catch { return TZConvert.GetTimeZoneInfo("Europe/Brussels"); }
        }

        // GET /api/admin/appointments/latest?includeCancelled=false
        [HttpGet("latest")]
        public async Task<IActionResult> Latest([FromQuery] bool includeCancelled = false)
        {
            var tz = Brussels();

            var query = _db.Appointments
                .Include(a => a.Service)
                .OrderByDescending(a => a.StartUtc)
                .AsQueryable();

            if (!includeCancelled)
                query = query.Where(a => a.Status != AppointmentStatus.Cancelled);

            var list = await query
                .Take(200)
                .Select(a => new
                {
                    id = a.Id,
                    startLocal = TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, tz).ToString("yyyy-MM-dd HH:mm"),
                    serviceName = a.Service != null ? a.Service.Name : $"#{a.ServiceId}",
                    clientName = a.ClientName,
                    clientEmail = a.ClientEmail,
                    clientPhone = a.ClientPhone,
                    status = a.Status.ToString()
                })
                .ToListAsync();

            return Ok(list);
        }

        // DELETE /api/admin/appointments/{id}?hard=true
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteOne(int id, [FromQuery] bool hard = false)
        {
            var appt = await _db.Appointments.FindAsync(id);
            if (appt == null) return NotFound();

            if (hard)
            {
                _db.Appointments.Remove(appt);
            }
            else
            {
                appt.Status = AppointmentStatus.Cancelled;
            }

            await _db.SaveChangesAsync();
            await _hub.Clients.All.SendAsync("bookingCancelled", new { appointmentId = id });

            return NoContent();
        }

        public class BulkIds { public List<int> Ids { get; set; } = new(); }

        // POST /api/admin/appointments/bulk-cancel?hard=true
        [HttpPost("bulk-cancel")]
        public async Task<IActionResult> BulkCancel([FromBody] BulkIds body, [FromQuery] bool hard = false)
        {
            if (body?.Ids == null || body.Ids.Count == 0)
                return BadRequest("No ids.");

            var appts = await _db.Appointments.Where(a => body.Ids.Contains(a.Id)).ToListAsync();
            if (hard)
            {
                _db.Appointments.RemoveRange(appts);
            }
            else
            {
                foreach (var a in appts) a.Status = AppointmentStatus.Cancelled;
            }

            var count = await _db.SaveChangesAsync();

            await _hub.Clients.All.SendAsync("bookingCancelled", new { ids = body.Ids });
            return Ok(new { ok = true, count });
        }
    }
}
