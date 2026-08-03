using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>
    /// Stable, localizable <see cref="Error"/> catalogue for the batch-upload module (Batch
    /// Upload Phased Plan, Phase 2). Mirrors <see cref="StockErrors"/>'s pattern.
    /// </summary>
    public static class BatchErrors
    {
        /// <summary>The decrypted upload file has no rows to process (→ 422).</summary>
        public static Error FileEmpty() =>
            Error.Validation("Batch.FileEmpty", "The uploaded file is empty.");

        /// <summary>
        /// The file could not be authenticated/decrypted (wrong key or tampered/corrupted
        /// ciphertext — AES-GCM tag check failed). Whole-file failure, not a per-row one (→ 422).
        /// </summary>
        public static Error DecryptionFailed() =>
            Error.Validation(
                "Batch.DecryptionFailed",
                "The file could not be decrypted. It may be corrupted or encrypted with the wrong key.");

        /// <summary>
        /// A file with this exact fingerprint (FileMac) was already uploaded for this tenant.
        /// No rows are written when this fires (→ 409).
        /// </summary>
        public static Error DuplicateFile() =>
            Error.Conflict("Batch.DuplicateFile", "This exact file has already been uploaded.");

        /// <summary>The caller-declared expected row count does not match the file's actual row count (→ 422).</summary>
        public static Error ExpectedRowCountMismatch(int expected, int actual) =>
            Error.Validation(
                "Batch.ExpectedRowCountMismatch",
                $"Expected {expected} rows but the file contains {actual}.")
                .WithArg($"{expected}/{actual}");

        /// <summary>
        /// The caller has no resolvable tenant context (e.g. a system-admin token, which this
        /// endpoint does not support — there is no tenant to upload cards for) (→ 401).
        /// </summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Batch.ActorNotResolved", "The acting principal could not be resolved.");

        /// <summary>
        /// An unexpected exception was caught at the orchestration boundary (Phase 6). Logged via
        /// Serilog with tenant/trace/batch context at the point it occurred; the client only ever
        /// sees this opaque message (→ 500).
        /// </summary>
        public static Error ProcessingFailed() =>
            Error.Internal(
                "Batch.ProcessingFailed",
                "An unexpected error occurred while processing the batch. Reference the trace id when reporting this.");
    }
}
