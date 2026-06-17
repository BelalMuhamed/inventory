namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Aggregates the application's service contracts behind a single injectable façade,
    /// keeping controller constructors small. Concrete service properties are added here as
    /// features are implemented.
    /// </summary>
    public interface IServiceManager
    {
        /// <summary>Authentication use cases (login, refresh, logout).</summary>
        IAuthService Auth { get; }
        /// <summary>Tenant management use cases (list, detail, create, update, password, delete, restore).</summary>
        ITenantService Tenants { get; }
        /// <summary>Branch management service.</summary>
        IBranchService Branches { get; }
        /// <summary>Product (catalog) management service.</summary>
        IProductService Products { get; }
    }
}
