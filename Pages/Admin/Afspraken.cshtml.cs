using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using TimeZoneConverter;

namespace ProHair.NL.Pages.Admin
{
    public class AfsprakenModel : PageModel
    {
        private readonly AppDbContext _db;
        public AfsprakenModel(AppDbContext db) { _db = db; }

        public record Row(string ServiceName, string ClientName, string ClientEmail, string ClientPhone, DateTime StartLocal, string Status);
        public List<Row> Items { get; set; } = new();

        public async Task OnGet()
        {
            var tz = TZConvert.GetTimeZoneInfo("Europe/Brussels");
            var list = await _db.Appointments
                .Include(a => a.Service)
                .Where(a => a.Status != ProHair.NL.Models.AppointmentStatus.Cancelled)
                .OrderByDescending(a => a.StartUtc)
                .Take(200)
                .ToListAsync();

            Items = list.Select(a => new Row(
                a.Service.Name,
                a.ClientName ?? "",
                a.ClientEmail ?? "",
                a.ClientPhone ?? "",
                TimeZoneInfo.ConvertTimeFromUtc(a.StartUtc, tz),
                a.Status.ToString()
            )).ToList();
        }
    }
}
