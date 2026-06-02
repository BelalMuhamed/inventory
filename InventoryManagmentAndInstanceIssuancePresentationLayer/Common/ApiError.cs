using System.Collections.Generic;

namespace InventoryManagmentAndInstanceIssuancePresentationLayer.Common
{
    /// <summary>
    /// Error payload returned inside <see cref="ApiResponse{T}"/> when an operation fails.
    /// Mirrors the domain <c>Error</c> (stable <see cref="Code"/>, human <see cref="Message"/>,
    /// <see cref="Category"/>) and optionally carries per-field <see cref="ValidationErrors"/>
    /// for 422 responses.
    /// </summary>
    public sealed class ApiError
    {
        /// <summary>Stable, machine-readable error identifier (e.g. "Branch.NotFound").</summary>
        public string Code { get; init; } = string.Empty;

        /// <summary>Human-readable description of the failure.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Error classification (e.g. "Validation", "Conflict", "NotFound").</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>
        /// Field-level errors, keyed by field name. Populated only for model-validation (422)
        /// failures; null otherwise so it is omitted from the serialized body.
        /// </summary>
        public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
    }
}
