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
    }
}
