// Models/Reservation.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace ProHair.NL.NL.Models
{
    public class Reservation
    {
        public int Id { get; set; }

        [Required, StringLength(80)]
        public string Name { get; set; } = "";

        [Required, EmailAddress, StringLength(120)]
        public string Email { get; set; } = "";

        [StringLength(30)]
        public string? Phone { get; set; }

        [Range(1, 20)]
        public int PartySize { get; set; } = 2;

        [StringLength(30)]
        public string? Location { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

        [StringLength(20)]
        public string Status { get; set; } = "Confirmed";
    }
}
