using System;

namespace DomainLayer.Common
{
    /// <summary>
    /// Base for entities that carry the ERD <c>[AUDIT]</c> block: creation/modification
    /// timestamps plus soft-delete markers. Hard-deleted entities (e.g. sessions) do not
    /// derive from this type.
    /// <para>
    /// Soft delete is expressed as <see cref="IsDeleted"/> + <see cref="DeletedAt"/>; a global
    /// query filter in the <c>DbContext</c> hides rows where <see cref="IsDeleted"/> is true,
    /// and a restore endpoint clears both fields.
    /// </para>
    /// </summary>
    public abstract class AuditableEntity
    {
        /// <summary>UTC instant the row was first created.</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>UTC instant of the most recent update, or <c>null</c> if never updated.</summary>
        public DateTime? UpdatedAt { get; set; }

        /// <summary>UTC instant the row was soft-deleted, or <c>null</c> while active.</summary>
        public DateTime? DeletedAt { get; set; }

        /// <summary>True when the row has been soft-deleted and must be hidden from normal queries.</summary>
        public bool IsDeleted { get; set; }
    }
}
