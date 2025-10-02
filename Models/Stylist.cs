using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.Models
{
    public class Stylist
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<WorkingHour> WorkingHours { get; set; } = new List<WorkingHour>();
        public ICollection<TimeOff> TimeOffs { get; set; } = new List<TimeOff>();
    }
}
