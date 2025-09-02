using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.Models
{
    public class TimeOff
    {
        public int Id { get; set; }
        public int StylistId { get; set; }
        public Stylist Stylist { get; set; } = null!;

        [Required] public DateTime StartLocal { get; set; }
        [Required] public DateTime EndLocal { get; set; }

        [MaxLength(200)] public string? Reason { get; set; }
    }
}
