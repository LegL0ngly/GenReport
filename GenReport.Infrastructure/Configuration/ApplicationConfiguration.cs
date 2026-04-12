using GenReport.Infrastructure.Interfaces;

namespace GenReport.Infrastructure.Configuration
{
    /// <summary>
    /// Represents the application configuration settings.
    /// </summary>
    public class ApplicationConfiguration : IApplicationConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether the database should be created.
        /// </summary>
        public bool CreateDB { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the database should be deleted.
        /// </summary>
        public bool DeleteDB { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the database should be seeded with data.
        /// </summary>
        public bool SeedDB { get; set; }

        /// <summary>
        /// Gets or sets the port number of the application.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Gets or sets the command timeout for database operations.
        /// </summary>
        public int CommandTimeOut { get; set; }

        /// <summary>
        /// Gets or sets the signing key used for issuing access tokens.
        /// </summary>
        public string IssuerSigningKey { get; set; } = "";

        /// <summary>
        /// Gets or sets the refresh key used for issuing refresh tokens.
        /// </summary>
        public string IssuerRefreshKey { get; set; } = "";

        /// <summary>
        /// Gets or sets the expiration time for access tokens (in seconds).
        /// </summary>
        public int AccessTokenExpiry { get; set; }

        /// <summary>
        /// Gets or sets the expiration time for refresh tokens (in seconds).
        /// </summary>
        public int RefreshTokenExpiry { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether URLs should be logged.
        /// </summary>
        public bool LogURLs { get; set; }

        /// <summary>
        /// Gets or sets the configuration settings for connecting to a RabbitMQ server.
        /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public RabbitMQConfiguration RabbitMQConfiguration { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        /// <summary>
        /// Gets or sets the hostname of the Go service.
        /// </summary>
        public string GoHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port the Go service is listening on.
        /// </summary>
        public int GoPort { get; set; }

        /// <summary>
        /// Gets or sets the path for the Go service test connection endpoint.
        /// </summary>
        public string GoTestConnectionPath { get; set; } = "/connections/test";

        /// <summary>
        /// Gets or sets the Base64-encoded 32-byte master key used by the credential encryption service.
        /// </summary>
        public string EncryptionMasterKey { get; set; } = "";

        /// <summary>
        /// Gets or sets the Cloudflare R2 storage configuration.
        /// Defaults to an unconfigured instance so <see cref="R2Configuration.IsConfigured"/> returns false.
        /// </summary>
        public R2Configuration R2Configuration { get; set; } = new();
    }
}