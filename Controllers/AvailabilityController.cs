using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Services;

namespace ProHair.NL.Controllers
{
    [Route("api/availability")]
    [ApiController]
    public class AvailabilityController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly BookingService _booking;

        public AvailabilityController(AppDbContext db, BookingService booking)
        { _db = db; _booking = booking; }

        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] DateTime date, [FromQuery] int stylistId, [FromQuery] int serviceId, [FromQuery] string tz = "Europe/Brussels")
        {
            var slots = await _booking.GetAvailableSlotsLocalAsync(date.Date, stylistId, serviceId, tz);
            return Ok(slots.Select(d => d.ToString("yyyy-MM-ddTHH:mm:ss")));
        }

        [HttpGet("config")]
        public async Task<IActionResult> Config()
        {
            var services = await _db.Services.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            var stylists = await _db.Stylists.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
            return Ok(new { services, stylists, timezone = "Europe/Brussels" });
        }
    }
}
