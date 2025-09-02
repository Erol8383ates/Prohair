using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.Models
{
    public class WorkingHour
    {
        public int Id { get; set; }
        public int StylistId { get; set; }
        public Stylist Stylist { get; set; } = null!;

        [Range(0, 6)]
        public int DayOfWeek { get; set; } // 0=Sunday..6=Saturday

        [Required] public TimeSpan StartLocal { get; set; }
        [Required] public TimeSpan EndLocal { get; set; }
    }
}
