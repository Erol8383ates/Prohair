namespace ProHair.NL.Services
{
    public class AdminAuthOptions
    {
        public string? Username { get; set; }
        public string? Password { get; set; }       // plain (dev only)
        public string? PasswordHash { get; set; }   // BCrypt optional
    }
}
