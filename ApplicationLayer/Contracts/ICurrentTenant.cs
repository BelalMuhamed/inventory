namespace ApplicationLayer.Contracts
{
    /// <summary>
    /// Ambient accessor for the authenticated principal, resolved from the current request's
    /// JWT claims. Used by the <c>DbContext</c> global query filter to scope tenant data and by
    /// services that need the caller's identity.
    /// <para>
    /// On unauthenticated paths (e.g. login) no principal is present: <see cref="TenantId"/> is
    /// <c>null</c> and <see cref="IsSystemAdmin"/> is <c>false</c>. The tenant query filter
    /// treats a null tenant as "match nothing" for tenant-scoped tables, which is safe because
    /// authentication reads the tenant through an explicitly unfiltered repository path.
    /// </para>
    /// </summary>
    public interface ICurrentTenant
    {
        /// <summary>Authenticated tenant's id, or <c>null</c> when unauthenticated or a system admin.</summary>
        long? TenantId { get; }

        /// <summary>True when the caller presented a system-admin token (bypasses tenant filters).</summary>
        bool IsSystemAdmin { get; }
    }
}
