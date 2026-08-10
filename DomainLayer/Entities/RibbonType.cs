namespace DomainLayer.Entities
{
    /// <summary>
    /// A ribbon type an Evolis print configuration can reference (Printing Module decision Q-05:
    /// converted from a free-text/enum field into a dedicated reference table so new ribbon types
    /// can be catalogued without a code change).
    /// <para>
    /// Confirmed global/tenant-agnostic and non-soft-deletable — the same shape as a lookup
    /// list — since ribbon types (e.g. YMCKO, YMCK, monochrome) describe a physical consumable
    /// standard shared across tenants rather than tenant-specific business data. No
    /// <c>TenantId</c>, no soft delete, no restore workflow.
    /// </para>
    /// </summary>
    public sealed class RibbonType
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Ribbon type name (e.g. "YMCKO", "Monochrome K"). Unique.</summary>
        public string Name { get; set; } = string.Empty;
    }
}
