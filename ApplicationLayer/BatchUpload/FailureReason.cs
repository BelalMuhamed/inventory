namespace ApplicationLayer.BatchUpload
{
    /// <summary>
    /// Why a batch-upload row failed (Batch Upload Phased Plan, Phase 6 per-row rules). The
    /// first three are produced by <see cref="IBatchRowParser"/> (Phase 4, no DB); the last two
    /// are produced by the orchestrator (Phase 6) after checking the row against the tenant's
    /// product/branch maps — declared here so both phases share one vocabulary.
    /// </summary>
    public enum FailureReason
    {
        /// <summary>Wrong field count, or a required field was empty.</summary>
        MalformedLine,

        /// <summary>Non-numeric, wrong length (not 13–19 digits), or failed the Luhn check.</summary>
        InvalidPan,

        /// <summary>The same PAN appeared earlier in this file; only the first occurrence proceeds.</summary>
        DuplicatePanInFile,

        /// <summary>The row's product name does not match any product in the tenant's catalog.</summary>
        UnknownProduct,

        /// <summary>The row's branch name does not match any branch in the tenant's catalog.</summary>
        UnknownBranch
    }
}
