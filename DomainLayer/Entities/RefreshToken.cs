using System;

namespace DomainLayer.Entities
{
    /// <summary>
    /// A persisted, rotatable refresh token backing the <c>/api/auth/refresh</c> and
    /// <c>/api/auth/logout</c> endpoints (API Spec §4.1). Only a hash of the token value is
    /// stored; the raw token is returned to the client once and never persisted.
    /// <para>
    /// A token belongs to exactly one principal: either a tenant (<see cref="TenantId"/> set)
    /// or the system admin (<see cref="SystemAdminId"/> set). On refresh the current token is
    /// revoked and a successor is issued, with <see cref="ReplacedByTokenHash"/> linking the two
    /// for audit and reuse-detection.
    /// </para>
    /// <para>
    /// <b>Schema note:</b> not present in the original ERD; added to support refresh/logout
    /// (approved). Flagged for DBA review.
    /// </para>
    /// </summary>
    public class RefreshToken
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning tenant, or <c>null</c> when the token belongs to the system admin.</summary>
        public long? TenantId { get; set; }

        /// <summary>Owning system admin, or <c>null</c> when the token belongs to a tenant.</summary>
        public long? SystemAdminId { get; set; }

        /// <summary>Hash of the opaque refresh-token value. The raw value is never stored.</summary>
        public string TokenHash { get; set; } = string.Empty;

        /// <summary>UTC instant the token was issued.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC instant the token expires and can no longer be exchanged.</summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>UTC instant the token was revoked (via rotation or logout), or <c>null</c> if still live.</summary>
        public DateTime? RevokedAt { get; set; }

        /// <summary>Hash of the token that superseded this one on rotation, or <c>null</c>.</summary>
        public string? ReplacedByTokenHash { get; set; }

        /// <summary>True when the token has neither been revoked nor expired at <paramref name="utcNow"/>.</summary>
        /// <param name="utcNow">The current UTC instant to evaluate against.</param>
        public bool IsActive(DateTime utcNow) => RevokedAt is null && utcNow < ExpiresAt;
    }
}
