namespace GenReport.Infrastructure.Configuration
{
    /// <summary>
    /// SMTP configuration options bound from the "Smtp" section of appsettings.json.
    /// </summary>
    public sealed class SmtpOptions
    {
        public const string SectionName = "Smtp";

        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1025;
        public string FromAddress { get; set; } = "noreply@genreport.app";
        public string FromName { get; set; } = "GenReport";
        public string? Username { get; set; }
        public string? Password { get; set; }
        public bool EnableSsl { get; set; } = false;
    }
}
