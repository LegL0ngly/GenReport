namespace GenReport.Infrastructure.Configuration
{
    /// <summary>
    /// Cloudflare R2 storage configuration.
    /// When <see cref="IsConfigured"/> is false the system falls back to email attachments.
    /// </summary>
    public class R2Configuration
    {
        /// <summary>Cloudflare Account ID (e.g. "abc123def456").</summary>
        public string AccountId { get; set; } = "";

        /// <summary>R2 bucket name.</summary>
        public string Bucket { get; set; } = "";

        /// <summary>R2 API token Access Key ID.</summary>
        public string AccessKeyId { get; set; } = "";

        /// <summary>R2 API token Secret Access Key.</summary>
        public string SecretAccessKey { get; set; } = "";

        /// <summary>
        /// Public base URL for the bucket (e.g. "https://cdn.example.com" or
        /// "https://pub-xxx.r2.dev").  Used to build the download link sent in emails.
        /// </summary>
        public string PublicUrl { get; set; } = "";

        /// <summary>
        /// Returns <c>true</c> when all required R2 fields are set and uploads should
        /// be attempted.  When <c>false</c> the system uses email attachments instead.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(AccountId)
            && !string.IsNullOrWhiteSpace(Bucket)
            && !string.IsNullOrWhiteSpace(AccessKeyId)
            && !string.IsNullOrWhiteSpace(SecretAccessKey)
            && !string.IsNullOrWhiteSpace(PublicUrl);
    }
}
