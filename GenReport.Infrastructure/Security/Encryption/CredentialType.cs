namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// Represents the type of credential to be encrypted or decrypted.
    /// </summary>
    public enum CredentialType
    {
        /// <summary>
        /// An API key credential.
        /// </summary>
        ApiKey,

        /// <summary>
        /// A user password credential.
        /// </summary>
        Password,

        /// <summary>
        /// A database connection string credential.
        /// </summary>
        ConnectionString
    }
}
