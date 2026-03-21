namespace GenReport.Infrastructure.Security.Encryption
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Factory that resolves the correct <see cref="ICredentialEncryptor"/>
    /// for a requested <see cref="CredentialType"/>.
    /// </summary>
    public sealed class CredentialEncryptorFactory : ICredentialEncryptorFactory
    {
        private readonly IReadOnlyDictionary<CredentialType, ICredentialEncryptor> _encryptors;

        /// <summary>
        /// Initialises the factory with all registered encryptors.
        /// </summary>
        /// <param name="encryptors">The set of encryptors to register.</param>
        public CredentialEncryptorFactory(IEnumerable<ICredentialEncryptor> encryptors)
        {
            var dict = new Dictionary<CredentialType, ICredentialEncryptor>();
            foreach (var encryptor in encryptors)
            {
                dict[encryptor.Type] = encryptor;
            }
            _encryptors = dict;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when no encryptor is registered for the given <paramref name="type"/>.
        /// </exception>
        public ICredentialEncryptor GetEncryptor(CredentialType type)
        {
            if (_encryptors.TryGetValue(type, out var encryptor))
                return encryptor;

            throw new ArgumentOutOfRangeException(
                nameof(type),
                $"No encryptor registered for credential type '{type}'. " +
                $"Registered types: {string.Join(", ", _encryptors.Keys)}");
        }
    }
}
