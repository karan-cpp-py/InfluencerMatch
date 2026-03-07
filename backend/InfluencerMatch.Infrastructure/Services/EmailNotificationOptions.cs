namespace InfluencerMatch.Infrastructure.Services
{
    public class EmailNotificationOptions
    {
        public bool Enabled { get; set; }
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = "InfluencerMatch";
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;
    }
}
