namespace GenReport.Tests
{
    using GenReport.Infrastructure.Security.Encryption;
    using System;
    using System.Security.Cryptography;

    /// <summary>
    /// Unit tests for the credential encryption service (factory pattern).
    /// These tests are pure unit tests — no DI, no database, no mocking required.
    /// </summary>
    [TestFixture]
    public class EncryptionServiceTests
    {
        // A valid 32-byte Base64 master key for tests
        private const string TestMasterKey = "K7gNU3sdo+OL0wNhqoVWhr3g6s1xYv72ol/pe/Unols=";

        private ICredentialEncryptorFactory _factory = null!;

        [SetUp]
        public void SetUp()
        {
            var encryptors = new ICredentialEncryptor[]
            {
                new ApiKeyEncryptor(TestMasterKey),
                new PasswordEncryptor(TestMasterKey),
                new ConnectionStringEncryptor(TestMasterKey)
            };
            _factory = new CredentialEncryptorFactory(encryptors);
        }

        // ─── Factory resolution ──────────────────────────────────────────────

        [Test]
        public void Factory_ReturnsApiKeyEncryptor_ForApiKeyType()
        {
            var encryptor = _factory.GetEncryptor(CredentialType.ApiKey);
            Assert.That(encryptor.Type, Is.EqualTo(CredentialType.ApiKey));
        }

        [Test]
        public void Factory_ReturnsPasswordEncryptor_ForPasswordType()
        {
            var encryptor = _factory.GetEncryptor(CredentialType.Password);
            Assert.That(encryptor.Type, Is.EqualTo(CredentialType.Password));
        }

        [Test]
        public void Factory_ReturnsConnectionStringEncryptor_ForConnectionStringType()
        {
            var encryptor = _factory.GetEncryptor(CredentialType.ConnectionString);
            Assert.That(encryptor.Type, Is.EqualTo(CredentialType.ConnectionString));
        }

        [Test]
        public void Factory_ThrowsArgumentOutOfRange_ForUnregisteredType()
        {
            // Cast an out-of-range int to the enum to simulate an unregistered type
            var factory = new CredentialEncryptorFactory(Array.Empty<ICredentialEncryptor>());
            Assert.Throws<ArgumentOutOfRangeException>(() => factory.GetEncryptor(CredentialType.ApiKey));
        }

        // ─── Encrypt / Decrypt round-trips ──────────────────────────────────

        [Test]
        [TestCase(CredentialType.ApiKey,           "sk-abcdef1234567890")]
        [TestCase(CredentialType.Password,          "P@ssw0rd!Secure#99")]
        [TestCase(CredentialType.ConnectionString,  "Server=localhost;Port=5432;Database=genreport;User Id=postgres;Password=postgres;")]
        public void Encrypt_ThenDecrypt_ReturnsOriginalPlaintext(CredentialType type, string plaintext)
        {
            var encryptor  = _factory.GetEncryptor(type);
            var ciphertext = encryptor.Encrypt(plaintext);
            var decrypted  = encryptor.Decrypt(ciphertext);

            Assert.That(decrypted, Is.EqualTo(plaintext));
        }

        [Test]
        [TestCase(CredentialType.ApiKey)]
        [TestCase(CredentialType.Password)]
        [TestCase(CredentialType.ConnectionString)]
        public void Encrypt_ProducesValidBase64_Output(CredentialType type)
        {
            var encryptor  = _factory.GetEncryptor(type);
            var ciphertext = encryptor.Encrypt("test-value");

            Assert.DoesNotThrow(() => Convert.FromBase64String(ciphertext),
                "Ciphertext should be valid Base64.");
        }

        [Test]
        [TestCase(CredentialType.ApiKey)]
        [TestCase(CredentialType.Password)]
        [TestCase(CredentialType.ConnectionString)]
        public void Encrypt_ProducesDifferentCiphertextOnEachCall(CredentialType type)
        {
            // AES-GCM uses a random nonce each time
            var encryptor   = _factory.GetEncryptor(type);
            var ciphertext1 = encryptor.Encrypt("same-value");
            var ciphertext2 = encryptor.Encrypt("same-value");

            Assert.That(ciphertext1, Is.Not.EqualTo(ciphertext2),
                "Each encryption call must produce a unique ciphertext (random nonce).");
        }

        // ─── Cross-type isolation ────────────────────────────────────────────

        [Test]
        public void Decrypt_WithWrongCredentialType_ThrowsCryptographicException()
        {
            // Encrypt as ApiKey, attempt to decrypt as Password (different sub-key)
            var apiKeyEncryptor   = _factory.GetEncryptor(CredentialType.ApiKey);
            var passwordEncryptor = _factory.GetEncryptor(CredentialType.Password);

            var ciphertext = apiKeyEncryptor.Encrypt("my-api-key-value");

            // AuthenticationTagMismatchException is a subclass of CryptographicException
            Assert.That(
                () => passwordEncryptor.Decrypt(ciphertext),
                Throws.InstanceOf<CryptographicException>(),
                "Decrypting with a different credential type's encryptor should fail authentication.");
        }

        [Test]
        public void Encrypt_DifferentCredentialTypes_ProduceDifferentCiphertexts()
        {
            const string sameValue = "same-secret-value";
            var apiCipher  = _factory.GetEncryptor(CredentialType.ApiKey).Encrypt(sameValue);
            var passCipher = _factory.GetEncryptor(CredentialType.Password).Encrypt(sameValue);

            // Different derived keys → fundamentally different ciphertexts
            Assert.That(apiCipher, Is.Not.EqualTo(passCipher));
        }

        // ─── Input validation ────────────────────────────────────────────────

        [Test]
        public void Encrypt_WithNullOrEmpty_ThrowsArgumentException(
            [Values(null, "")] string? input)
        {
            var encryptor = _factory.GetEncryptor(CredentialType.ApiKey);
            // ArgumentNullException (for null) is a subclass of ArgumentException
            Assert.That(
                () => encryptor.Encrypt(input!),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Decrypt_WithNullOrEmpty_ThrowsArgumentException(
            [Values(null, "")] string? input)
        {
            var encryptor = _factory.GetEncryptor(CredentialType.ApiKey);
            // ArgumentNullException (for null) is a subclass of ArgumentException
            Assert.That(
                () => encryptor.Decrypt(input!),
                Throws.InstanceOf<ArgumentException>());
        }

        [Test]
        public void Decrypt_WithTamperedCiphertext_ThrowsCryptographicException()
        {
            var encryptor  = _factory.GetEncryptor(CredentialType.ApiKey);
            var ciphertext = encryptor.Encrypt("original-value");

            // Flip a byte in the packed data to simulate tampering
            var bytes = Convert.FromBase64String(ciphertext);
            bytes[bytes.Length - 1] ^= 0xFF;
            var tampered = Convert.ToBase64String(bytes);

            // AuthenticationTagMismatchException is a subclass of CryptographicException
            Assert.That(
                () => encryptor.Decrypt(tampered),
                Throws.InstanceOf<CryptographicException>(),
                "AES-GCM authentication tag verification must reject tampered ciphertext.");
        }
    }
}
