using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ProHair.NL.Hubs;
using ProHair.NL.Services;
using System;
using System.Threading.Tasks;

namespace ProHair.NL.Controllers
{
    [Route("api/appointments")]
    [ApiController]
    public class AppointmentsController : ControllerBase
    {
        private readonly BookingService _booking;
        private readonly EmailService _email;
        private readonly IHubContext<BookingHub> _hub;
        private readonly ILogger<AppointmentsController> _log;

        public AppointmentsController(BookingService booking, EmailService email, IHubContext<BookingHub> hub, ILogger<AppointmentsController> log)
        {
            _booking = booking; _email = email; _hub = hub; _log = log;
        }

        public record HoldRequest(int StylistId, int ServiceId, DateTime StartLocal, string Tz);
        public record HoldResponse(bool Ok, string? Error, string? HoldToken, DateTime? ExpiresAtUtc);

        [HttpPost("hold")]
        public async Task<ActionResult<HoldResponse>> Hold([FromBody] HoldRequest req)
        {
            var (ok, err, hold) = await _booking.CreateHoldAsync(req.StylistId, req.ServiceId, req.StartLocal, req.Tz);
            if (!ok || hold == null) return Ok(new HoldResponse(false, err, null, null));

            await _hub.Clients.All.SendAsync("slotHeld", new { stylistId = req.StylistId, serviceId = req.ServiceId, startLocal = req.StartLocal.ToString("s") });
            return Ok(new HoldResponse(true, null, hold.HoldToken, hold.HoldUntilUtc));
        }

        public record ConfirmRequest(string HoldToken, string ClientName, string ClientEmail, string ClientPhone);
        public record SimpleResponse(bool Ok, string? Error);

        [HttpPost("confirm")]
        public async Task<ActionResult<SimpleResponse>> Confirm([FromBody] ConfirmRequest req)
        {
            var (ok, err, appt) = await _booking.ConfirmAsync(req.HoldToken, req.ClientName, req.ClientEmail, req.ClientPhone);
            if (!ok || appt is null)
            {
                _log.LogWarning("Booking confirm failed: {Err}", err);
                return Ok(new SimpleResponse(false, err ?? "Niet gelukt."));
            }

            // Notify UI first (don’t block on email)
            await _hub.Clients.All.SendAsync("slotBooked", new { stylistId = appt.StylistId, serviceId = appt.ServiceId, startUtc = appt.StartUtc });
            await _hub.Clients.All.SendAsync("bookingCreated", new { appointmentId = appt.Id });

            // Fire-and-forget email, with logging
            _ = Task.Run(async () =>
            {
                try { await _email.SendBookingConfirmationAsync(appt); }
                catch (Exception ex) { _log.LogError(ex, "Confirmation email send failed for appointment {Id}", appt.Id); }
            });

            return Ok(new SimpleResponse(true, null));
        }

        public record ReleaseRequest(string HoldToken);

        [HttpPost("release")]
        public async Task<ActionResult<SimpleResponse>> Release([FromBody] ReleaseRequest req)
        {
            var (ok, err) = await _booking.ReleaseHoldAsync(req.HoldToken);
            if (ok) await _hub.Clients.All.SendAsync("slotReleased", new { holdToken = req.HoldToken });
            else _log.LogWarning("ReleaseHold failed: {Err}", err);

            return Ok(new SimpleResponse(ok, err));
        }
    }
}
