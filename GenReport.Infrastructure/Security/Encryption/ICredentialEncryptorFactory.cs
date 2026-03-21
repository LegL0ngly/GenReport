namespace GenReport.Infrastructure.Security.Encryption
{
    /// <summary>
    /// Defines the contract for a factory that resolves the appropriate
    /// <see cref="ICredentialEncryptor"/> for a given <see cref="CredentialType"/>.
    /// </summary>
    public interface ICredentialEncryptorFactory
    {
        /// <summary>
        /// Returns the encryptor registered for the specified credential type.
        /// </summary>
        /// <param name="type">The type of credential to encrypt or decrypt.</param>
        /// <returns>The matching <see cref="ICredentialEncryptor"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when no encryptor is registered for the given <paramref name="type"/>.
        /// </exception>
        ICredentialEncryptor GetEncryptor(CredentialType type);
    }
}
