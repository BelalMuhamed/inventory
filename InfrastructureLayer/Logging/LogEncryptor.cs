// InfrastructureLayer/Logging/LogEncryptor.cs
using System;
using System.Security.Cryptography;
using System.Text;

namespace InfrastructureLayer.Logging
{
    /// <summary>
    /// Encrypts a log line with AES-GCM using a key derived from the configured password (PBKDF2).
    /// Output per line is Base64 of [12-byte nonce][16-byte tag][ciphertext], so each line is
    /// independently decryptable and the file is unreadable without the password.
    /// </summary>
    public sealed class LogEncryptor
    {
        private const int NonceSize = 12;
        private const int TagSize = 16;
        private const int KeySize = 32;       // AES-256
        private const int Pbkdf2Iterations = 100_000;

        private readonly byte[] _key;

        /// <summary>Derives the AES key from the password and salt.</summary>
        /// <param name="password">Secret password (from configuration/secrets).</param>
        /// <param name="salt">Stable salt (from configuration/secrets).</param>
        public LogEncryptor(string password, byte[] salt)
        {
            using var kdf = new Rfc2898DeriveBytes(password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256);
            _key = kdf.GetBytes(KeySize);
        }

        /// <summary>Encrypts one log line and returns a Base64 token safe to append to a text file.</summary>
        /// <param name="plaintext">The rendered log line.</param>
        public string EncryptLine(string plaintext)
        {
            byte[] data = Encoding.UTF8.GetBytes(plaintext);
            byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
            byte[] cipher = new byte[data.Length];
            byte[] tag = new byte[TagSize];

            using (var aes = new AesGcm(_key, TagSize))
            {
                aes.Encrypt(nonce, data, cipher, tag);
            }

            byte[] output = new byte[NonceSize + TagSize + cipher.Length];
            Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
            Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
            Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);
            return Convert.ToBase64String(output);
        }
    }
}