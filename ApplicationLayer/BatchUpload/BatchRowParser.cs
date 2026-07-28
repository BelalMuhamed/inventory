using System;
using System.Collections.Generic;
using System.Linq;
using ApplicationLayer.Contracts;
using ApplicationLayer.Security;

namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// Default <see cref="IBatchRowParser"/> implementation (Batch Upload Phased Plan, Phase 4).
    /// Pure logic only — no DB, no I/O — which is why it lives in ApplicationLayer directly
    /// rather than behind an InfrastructureLayer implementation: there is no external dependency
    /// to abstract away. The interface still exists for DI/testability (the orchestrator depends
    /// on the abstraction, not this type, per the project's constructor-injection convention).
    /// </summary>
    public sealed class BatchRowParser : IBatchRowParser
    {
        private const char FieldDelimiter = '|';
        private const int RequiredFieldCount = 3;
        private const int MinPanLength = 13;
        private const int MaxPanLength = 19;

        /// <inheritdoc />
        public (IReadOnlyList<ParsedBatchRow> ValidRows, IReadOnlyList<FailedBatchRow> FailedRows) Parse(string fileContent)
        {
            var validRows = new List<ParsedBatchRow>();
            var failedRows = new List<FailedBatchRow>();

            if (string.IsNullOrEmpty(fileContent))
            {
                return (validRows, failedRows);
            }

            string[] lines = fileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var seenPans = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < lines.Length; i++)
            {
                // A trailing newline at end-of-file produces one empty final element that is not
                // a real row — drop only that one. A blank line anywhere else is a real
                // (malformed) row and must still be reported.
                if (i == lines.Length - 1 && lines[i].Length == 0)
                {
                    continue;
                }

                int rowNumber = i + 1;
                string[] fields = lines[i].Split(FieldDelimiter);

                if (fields.Length != RequiredFieldCount || fields.Any(string.IsNullOrWhiteSpace))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, "N/A", FailureReason.MalformedLine));
                    continue;
                }

                string pan = fields[0].Trim().Replace(" ", string.Empty);
                string productName = fields[1].Trim();
                string branchName = fields[2].Trim();

                if (!IsValidPan(pan))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, PanMasker.Mask(pan), FailureReason.InvalidPan));
                    continue;
                }

                if (!seenPans.Add(pan))
                {
                    failedRows.Add(new FailedBatchRow(rowNumber, PanMasker.Mask(pan), FailureReason.DuplicatePanInFile));
                    continue;
                }

                validRows.Add(new ParsedBatchRow(rowNumber, pan, productName, branchName));
            }

            return (validRows, failedRows);
        }

        private static bool IsValidPan(string pan)
        {
            if (pan.Length is < MinPanLength or > MaxPanLength)
            {
                return false;
            }

            return pan.All(char.IsDigit) && PassesLuhnCheck(pan);
        }

        // Standard Luhn (mod-10) checksum: from the rightmost digit, double every second digit;
        // if doubling pushes a digit above 9, subtract 9 (equivalent to summing its two digits).
        // Valid when the total is a multiple of 10.
        private static bool PassesLuhnCheck(string digits)
        {
            int sum = 0;
            bool doubleDigit = false;

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int digit = digits[i] - '0';

                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9)
                    {
                        digit -= 9;
                    }
                }

                sum += digit;
                doubleDigit = !doubleDigit;
            }

            return sum % 10 == 0;
        }
    }
}
