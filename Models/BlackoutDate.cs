namespace ProHair.NL.Models
{
    public class BlackoutDate
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }      // yyyy-MM-dd
        public string? Reason { get; set; }
    }
}
