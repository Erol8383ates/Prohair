using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.Models
{
    public class Service
    {
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Range(5, 480)]
        public int DurationMinutes { get; set; }

        [Range(0, 10000)]
        public decimal Price { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
