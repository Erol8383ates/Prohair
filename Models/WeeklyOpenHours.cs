using System;

namespace ProHair.NL.Models
{
    public class WeeklyOpenHours
    {
        public int Id { get; set; }
        public DayOfWeek Day { get; set; }      // Sunday..Saturday
        public bool IsClosed { get; set; }
        public TimeOnly? Open { get; set; }     // 10:00
        public TimeOnly? Close { get; set; }    // 19:00
    }
}
