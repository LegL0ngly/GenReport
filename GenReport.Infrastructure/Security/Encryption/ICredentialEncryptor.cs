namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// Defines the contract for a credential encryptor that can encrypt and decrypt
    /// a specific type of credential.
    /// </summary>
    public interface ICredentialEncryptor
    {
        /// <summary>
        /// Gets the credential type this encryptor handles.
        /// </summary>
        CredentialType Type { get; }

        /// <summary>
        /// Encrypts the given plaintext credential value.
        /// </summary>
        /// <param name="plaintext">The raw credential value to encrypt.</param>
        /// <returns>A Base64-encoded ciphertext string (nonce + tag + ciphertext).</returns>
        string Encrypt(string plaintext);

        /// <summary>
        /// Decrypts a previously encrypted credential value.
        /// </summary>
        /// <param name="ciphertext">A Base64-encoded string produced by <see cref="Encrypt"/>.</param>
        /// <returns>The original plaintext credential value.</returns>
        string Decrypt(string ciphertext);
    }
}
