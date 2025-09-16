using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using ProHair.NL.Data;
using ProHair.NL.Models;
using ProHair.NL.Hubs;
using ProHair.NL.Services; // CacheKeys (opsiyonel)

namespace ProHair.NL.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")]
    public class AdminAvailabilityModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<BookingHub> _hub;
        private readonly IMemoryCache _cache;

        public AdminAvailabilityModel(AppDbContext db, IHubContext<BookingHub> hub, IMemoryCache cache)
        {
            _db = db; _hub = hub; _cache = cache;
        }

        [BindProperty] public List<WeeklyOpenHours> Weekly { get; set; } = new();
        public List<BlackoutDate> Blackouts { get; set; } = new();

        [BindProperty] public DateOnly NewBlackoutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [BindProperty] public string? NewBlackoutReason { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Weekly = await _db.WeeklyOpenHours.OrderBy(x => x.Day).ToListAsync();
            Blackouts = await _db.BlackoutDates.OrderBy(x => x.Date).ToListAsync();
            if (NewBlackoutDate == default) NewBlackoutDate = DateOnly.FromDateTime(DateTime.Today);
            return Page();
        }

        public async Task<IActionResult> OnPostSaveWeeklyAsync()
        {
            if (!ModelState.IsValid)
            {
                await OnGetAsync();
                return Page();
            }

            // Güvenli update (DB'den çek → alanları ata)
            foreach (var row in Weekly)
            {
                var dbRow = await _db.WeeklyOpenHours.FirstOrDefaultAsync(w => w.Id == row.Id);
                if (dbRow == null) continue;

                dbRow.IsClosed = row.IsClosed;
                dbRow.Open = row.Open;
                dbRow.Close = row.Close;
            }

            await _db.SaveChangesAsync();

            // Cache invalidate (opsiyonel)
            _cache.Set(CacheKeys.Stamp, Guid.NewGuid().ToString());

            // 🔔 Tüm client'lara bildir
            await _hub.Clients.All.SendAsync("calendarChanged");

            TempData["Ok"] = "Çalışma saatleri güncellendi.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostAddBlackoutAsync()
        {
            if (NewBlackoutDate == default)
                NewBlackoutDate = DateOnly.FromDateTime(DateTime.Today);

            var exists = await _db.BlackoutDates.AnyAsync(b => b.Date == NewBlackoutDate);
            if (!exists)
            {
                _db.BlackoutDates.Add(new BlackoutDate
                {
                    Date = NewBlackoutDate,
                    Reason = NewBlackoutReason
                });
                await _db.SaveChangesAsync();
            }

            _cache.Set(CacheKeys.Stamp, Guid.NewGuid().ToString());
            await _hub.Clients.All.SendAsync("calendarChanged");

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteBlackoutAsync(int id)
        {
            var b = await _db.BlackoutDates.FindAsync(id);
            if (b != null)
            {
                _db.BlackoutDates.Remove(b);
                await _db.SaveChangesAsync();
            }

            _cache.Set(CacheKeys.Stamp, Guid.NewGuid().ToString());
            await _hub.Clients.All.SendAsync("calendarChanged");

            return RedirectToPage();
        }
    }
}
