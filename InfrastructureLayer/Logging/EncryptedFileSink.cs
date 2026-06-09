// InfrastructureLayer/Logging/EncryptedFileSink.cs
using System;
using System.IO;
using System.Text;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;

namespace InfrastructureLayer.Logging
{
    /// <summary>
    /// Serilog sink that renders each event with the supplied formatter, encrypts the line via
    /// <see cref="LogEncryptor"/>, and appends it to a file. Writes are serialized with a lock so
    /// concurrent log calls don't interleave bytes.
    /// </summary>
    public sealed class EncryptedFileSink : ILogEventSink
    {
        private readonly string _filePath;
        private readonly LogEncryptor _encryptor;
        private readonly ITextFormatter _formatter;
        private readonly object _gate = new();

        /// <summary>Creates the sink for a target file path.</summary>
        public EncryptedFileSink(string filePath, LogEncryptor encryptor, ITextFormatter formatter)
        {
            _filePath = filePath;
            _encryptor = encryptor;
            _formatter = formatter;
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            using var buffer = new StringWriter();
            _formatter.Format(logEvent, buffer);
            string encrypted = _encryptor.EncryptLine(buffer.ToString().TrimEnd('\r', '\n'));

            lock (_gate)
            {
                File.AppendAllText(_filePath, encrypted + Environment.NewLine, Encoding.UTF8);
            }
        }
    }
}