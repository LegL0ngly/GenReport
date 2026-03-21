namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// AES-256-GCM encryptor for <see cref="CredentialType.ApiKey"/> credentials.
    /// </summary>
    public sealed class ApiKeyEncryptor : AesGcmEncryptorBase
    {
        /// <inheritdoc/>
        public override CredentialType Type => CredentialType.ApiKey;

        /// <summary>
        /// Initialises a new <see cref="ApiKeyEncryptor"/> using the application master key.
        /// </summary>
        /// <param name="masterKey">Base64-encoded 32-byte master key from configuration.</param>
        public ApiKeyEncryptor(string masterKey) : base(masterKey) { }
    }
}
