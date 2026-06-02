namespace ApplicationLayer.ServicesContracts
{
    /// <summary>
    /// Aggregates the application's service contracts behind a single injectable façade,
    /// keeping controller constructors small. Concrete service properties are added here as
    /// features are implemented (e.g. <c>IUserService Users { get; }</c>).
    /// </summary>
    public interface IServiceManager
    {
    }
}
