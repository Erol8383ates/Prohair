using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;

namespace ProHair.NL.Pages
{
    // PageModel seviyesinde uygulanır
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class ReserverenModel : PageModel
    {
        private readonly AppDbContext _db;
        public ReserverenModel(AppDbContext db) => _db = db;

        // /Reserveren?handler=DisabledDates&year=YYYY&month=M
        public async Task<IActionResult> OnGetDisabledDates(int year, int month)
        {
            // Ara cache/proxy’leri de kapat
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
            Response.Headers["Pragma"] = "no-cache";

            var closedDows = await _db.WeeklyOpenHours
                .AsNoTracking()
                .Where(w => w.IsClosed || w.Open == null || w.Close == null)
                .Select(w => (int)w.Day) // 0=Sun..6=Sat
                .ToListAsync();

            var blackouts = await _db.BlackoutDates
                .AsNoTracking()
                .Where(b => b.Date.Year == year && b.Date.Month == month)
                .Select(b => b.Date.ToString("yyyy-MM-dd"))
                .ToListAsync();

            return new JsonResult(new { closedDows, blackouts });
        }
    }
}
