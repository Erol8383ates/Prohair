using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;

namespace ProHair.NL.Pages
{
    public class ReserverenModel : PageModel
    {
        private readonly AppDbContext _db;

        public ReserverenModel(AppDbContext db)
        {
            _db = db;
        }

        public void OnGet() { }

        // Returns { closedDows: int[], blackouts: string[] } for the visible month
        // JS uses it to disable weekdays and specific dates in flatpickr
        public async Task<IActionResult> OnGetDisabledDates(int year, int month)
        {
            // closed weekdays from WeeklyOpenHours
            var weekly = await _db.WeeklyOpenHours.ToListAsync();
            var closedDows = weekly.Where(w => w.IsClosed).Select(w => (int)w.Day).ToArray();

            // one-off blackout dates within requested month
            var blackouts = await _db.BlackoutDates
                .Where(b => b.Date.Year == year && b.Date.Month == month)
                .Select(b => b.Date.ToString("yyyy-MM-dd"))
                .ToListAsync();

            return new JsonResult(new { closedDows, blackouts });
        }
    }
}
