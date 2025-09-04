using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;

namespace ProHair.NL.Pages.Admin
{
    [Authorize(Policy = "AdminOnly")] // or [Authorize(Roles = "Admin")]
    public class AdminAvailabilityModel : PageModel
    {
        private readonly AppDbContext _db;
        public AdminAvailabilityModel(AppDbContext db) => _db = db;

        [BindProperty] public List<WeeklyOpenHours> Weekly { get; set; } = new();
        public List<BlackoutDate> Blackouts { get; set; } = new();

        // Pre-fill to today so the date input is never 0001-01-01
        [BindProperty] public DateOnly NewBlackoutDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        [BindProperty] public string? NewBlackoutReason { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            Weekly = await _db.WeeklyOpenHours
                              .OrderBy(x => x.Day)
                              .ToListAsync();

            Blackouts = await _db.BlackoutDates
                                 .OrderBy(x => x.Date)
                                 .ToListAsync();

            if (NewBlackoutDate == default)
                NewBlackoutDate = DateOnly.FromDateTime(DateTime.Today);

            return Page();
        }

        public async Task<IActionResult> OnPostSaveWeeklyAsync()
        {
            if (!ModelState.IsValid)
            {
                // re-load lists so the page renders correctly with validation messages
                await OnGetAsync();
                return Page();
            }

            foreach (var row in Weekly)
            {
                _db.Update(row);
            }

            await _db.SaveChangesAsync();
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
            return RedirectToPage();
        }
    }
}
