namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// AES-256-GCM encryptor for <see cref="CredentialType.ConnectionString"/> credentials.
    /// </summary>
    public sealed class ConnectionStringEncryptor : AesGcmEncryptorBase
    {
        /// <inheritdoc/>
        public override CredentialType Type => CredentialType.ConnectionString;

        /// <summary>
        /// Initialises a new <see cref="ConnectionStringEncryptor"/> using the application master key.
        /// </summary>
        /// <param name="masterKey">Base64-encoded 32-byte master key from configuration.</param>
        public ConnectionStringEncryptor(string masterKey) : base(masterKey) { }
    }
}
