namespace GenReport.Infrastructure.Security.Encryption
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Abstract base class that implements AES-256-GCM encryption for a specific
    /// <see cref="CredentialType"/>. Each concrete subclass produces its own
    /// sub-key derived from the master key and the credential type label,
    /// so credentials of different types cannot be cross-decrypted.
    /// </summary>
    /// <remarks>
    /// Ciphertext format (Base64-encoded): <c>[ nonce (12 bytes) | tag (16 bytes) | ciphertext (N bytes) ]</c>
    /// </remarks>
    public abstract class AesGcmEncryptorBase : ICredentialEncryptor
    {
        private const int NonceSize = 12;   // 96-bit nonce recommended for AES-GCM
        private const int TagSize   = 16;   // 128-bit authentication tag
        private const int KeySize   = 32;   // AES-256

        private readonly byte[] _derivedKey;

        /// <summary>
        /// Initialises the encryptor by deriving a type-specific sub-key from
        /// the master key using HKDF-SHA256.
        /// </summary>
        /// <param name="masterKey">
        ///   A 32-byte (256-bit) Base64-encoded master key from application config.
        /// </param>
        protected AesGcmEncryptorBase(string masterKey)
        {
            var masterKeyBytes = Convert.FromBase64String(masterKey);
            var info           = Encoding.UTF8.GetBytes(Type.ToString());

            // Derive a unique sub-key per credential type so different
            // types cannot decrypt each other's ciphertext.
            _derivedKey = HKDF.DeriveKey(
                hashAlgorithmName: HashAlgorithmName.SHA256,
                ikm            : masterKeyBytes,
                outputLength   : KeySize,
                salt           : null,
                info           : info);
        }

        /// <inheritdoc/>
        public abstract CredentialType Type { get; }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="plaintext"/> is null or empty.</exception>
        public string Encrypt(string plaintext)
        {
            ArgumentException.ThrowIfNullOrEmpty(plaintext, nameof(plaintext));

            var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
            var nonce          = new byte[NonceSize];
            var tag            = new byte[TagSize];
            var ciphertext     = new byte[plaintextBytes.Length];

            RandomNumberGenerator.Fill(nonce);

            using var aesGcm = new AesGcm(_derivedKey, TagSize);
            aesGcm.Encrypt(nonce, plaintextBytes, ciphertext, tag);

            // Pack: nonce | tag | ciphertext
            var packed = new byte[NonceSize + TagSize + ciphertext.Length];
            Buffer.BlockCopy(nonce,      0, packed, 0,                          NonceSize);
            Buffer.BlockCopy(tag,        0, packed, NonceSize,                  TagSize);
            Buffer.BlockCopy(ciphertext, 0, packed, NonceSize + TagSize,        ciphertext.Length);

            return Convert.ToBase64String(packed);
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="ciphertext"/> is null or empty.</exception>
        /// <exception cref="CryptographicException">Thrown when the ciphertext has been tampered with or is invalid.</exception>
        public string Decrypt(string ciphertext)
        {
            ArgumentException.ThrowIfNullOrEmpty(ciphertext, nameof(ciphertext));

            var packed = Convert.FromBase64String(ciphertext);

            if (packed.Length < NonceSize + TagSize)
                throw new CryptographicException("Ciphertext is too short to be valid.");

            var nonce          = packed[..NonceSize];
            var tag            = packed[NonceSize..(NonceSize + TagSize)];
            var encryptedBytes = packed[(NonceSize + TagSize)..];
            var plaintextBytes = new byte[encryptedBytes.Length];

            using var aesGcm = new AesGcm(_derivedKey, TagSize);
            aesGcm.Decrypt(nonce, encryptedBytes, tag, plaintextBytes);

            return Encoding.UTF8.GetString(plaintextBytes);
        }
    }
}
