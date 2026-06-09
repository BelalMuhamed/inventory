// ApplicationLayer/Options/LogEncryptionOptions.cs
namespace ApplicationLayer.Options
{
    /// <summary>
    /// Settings for encrypted file logging, bound from the <c>"LogEncryption"</c> section. The
    /// password is supplied via user-secrets/environment (never committed), exactly like the JWT key.
    /// </summary>
    public sealed class LogEncryptionOptions
    {
        /// <summary>Configuration section name.</summary>
        public const string SectionName = "LogEncryption";

        /// <summary>Password the AES key is derived from (PBKDF2). Supplied via secrets.</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>Base64 salt for key derivation. Supplied via secrets; stable so logs stay decryptable.</summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>Directory for the encrypted log files. Defaults to <c>logs</c> under the content root.</summary>
        public string Directory { get; set; } = "logs";

        /// <summary>Encrypted error-log file name (Warning+ without an exception).</summary>
        public string ErrorFileName { get; set; } = "errors.log.enc";

        /// <summary>Encrypted exception-log file name (entries carrying an exception).</summary>
        public string ExceptionFileName { get; set; } = "exceptions.log.enc";
    }
}