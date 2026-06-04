using System;
using ApplicationLayer.ServicesContracts;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Concrete service façade. Resolves each service lazily so a controller depending on
    /// <see cref="IServiceManager"/> only constructs the services it actually uses.
    /// </summary>
    public sealed class ServiceManager : IServiceManager
    {
        private readonly Lazy<IAuthService> _auth;
        private readonly Lazy<ITenantService> _tenants;

        /// <summary>Creates the façade from lazily-resolved service factories.</summary>
        /// <param name="authFactory">Factory that produces the authentication service.</param>
        /// <param name="tenantFactory">Factory that produces the tenant management service.</param>
        public ServiceManager(Func<IAuthService> authFactory, Func<ITenantService> tenantFactory)
        {
            _auth = new Lazy<IAuthService>(authFactory);
            _tenants = new Lazy<ITenantService>(tenantFactory);
        }

        /// <inheritdoc />
        public IAuthService Auth => _auth.Value;

        /// <inheritdoc />
        public ITenantService Tenants => _tenants.Value;
    }
}
