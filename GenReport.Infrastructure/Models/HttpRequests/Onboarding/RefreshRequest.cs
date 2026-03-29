namespace GenReport.Infrastructure.Models.HttpRequests.Onboarding
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Defines the <see cref="RefreshRequest" />
    /// </summary>
    public class RefreshRequest
    {
        /// <summary>
        /// Gets or sets the RefreshToken
        /// </summary>
        [JsonPropertyName("refreshToken")]
        public required string RefreshToken { get; set; }
    }
}
