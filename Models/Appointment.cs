using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        public int ServiceId { get; set; }
        public Service Service { get; set; } = null!;

        public int StylistId { get; set; }
        public Stylist Stylist { get; set; } = null!;

        [Required] public DateTime StartUtc { get; set; }
        [Required] public DateTime EndUtc { get; set; }

        public AppointmentStatus Status { get; set; } = AppointmentStatus.Hold;

        [MaxLength(120)] public string? ClientName { get; set; }
        [MaxLength(120)] public string? ClientEmail { get; set; }
        [MaxLength(40)]  public string? ClientPhone { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime? HoldUntilUtc { get; set; }
        [MaxLength(64)] public string? HoldToken { get; set; }
    }
}
