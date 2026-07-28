using System;
using System.Security.Cryptography;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using DomainLayer.Common;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// AES-256-GCM implementation of <see cref="IBatchFileCipher"/> (Batch Upload Phased Plan,
    /// Phase 2). Derives a distinct key per tenant from the configured master secret via PBKDF2 —
    /// the derived key is computed on demand and never stored. Ciphertext layout matches
    /// <see cref="InfrastructureLayer.Logging.LogEncryptor"/>'s convention: [12-byte nonce]
    /// [16-byte GCM tag][ciphertext].
    /// </summary>
    public sealed class BatchFileCipher : IBatchFileCipher
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;       // AES-256
        private const int Pbkdf2Iterations = 100_000;

        private readonly BatchCipherOptions _options;

        /// <summary>Creates the cipher bound to the configured master secret/salt.</summary>
        /// <param name="options">Batch-cipher settings (user-secrets/environment-backed).</param>
        public BatchFileCipher(IOptions<BatchCipherOptions> options)
        {
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public Result<string> Decrypt(long tenantId, byte[] ciphertext)
        {
            if (ciphertext is null || ciphertext.Length < NonceSize + TagSize)
            {
                return Result.Failure<string>(BatchErrors.DecryptionFailed());
            }

            try
            {
                byte[] key = DeriveTenantKey(tenantId);

                ReadOnlySpan<byte> input = ciphertext;
                ReadOnlySpan<byte> nonce = input[..NonceSize];
                ReadOnlySpan<byte> tag = input.Slice(NonceSize, TagSize);
                ReadOnlySpan<byte> cipher = input[(NonceSize + TagSize)..];

                byte[] plaintext = new byte[cipher.Length];

                using (var aes = new AesGcm(key, TagSize))
                {
                    aes.Decrypt(nonce, cipher, tag, plaintext);
                }

                return Result.Success(Encoding.UTF8.GetString(plaintext));
            }
            catch (CryptographicException)
            {
                // Wrong key or tampered/corrupted ciphertext — GCM tag check failed. Expected
                // failure mode per the plan: a clean Result, not a bubbled exception.
                return Result.Failure<string>(BatchErrors.DecryptionFailed());
            }
        }

        // Combines the configured salt with the tenant id so the same master secret yields a
        // distinct key per tenant. Computed fresh on every call — never cached, never persisted.
        private byte[] DeriveTenantKey(long tenantId)
        {
            byte[] configuredSalt = Convert.FromBase64String(_options.Salt);
            byte[] tenantSalt = new byte[configuredSalt.Length + sizeof(long)];
            Buffer.BlockCopy(configuredSalt, 0, tenantSalt, 0, configuredSalt.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(tenantId), 0, tenantSalt, configuredSalt.Length, sizeof(long));

            using var kdf = new Rfc2898DeriveBytes(_options.MasterSecret, tenantSalt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            return kdf.GetBytes(KeySize);
        }
    }
}
