public class SmtpOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string User { get; set; } = "";
    public string Pass { get; set; } = "";
    public string From { get; set; } = ""; // "ProHair Studio erolibili83@gmail.com"
}
