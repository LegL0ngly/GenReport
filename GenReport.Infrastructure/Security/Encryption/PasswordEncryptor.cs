namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// AES-256-GCM encryptor for <see cref="CredentialType.Password"/> credentials.
    /// </summary>
    public sealed class PasswordEncryptor : AesGcmEncryptorBase
    {
        /// <inheritdoc/>
        public override CredentialType Type => CredentialType.Password;

        /// <summary>
        /// Initialises a new <see cref="PasswordEncryptor"/> using the application master key.
        /// </summary>
        /// <param name="masterKey">Base64-encoded 32-byte master key from configuration.</param>
        public PasswordEncryptor(string masterKey) : base(masterKey) { }
    }
}
