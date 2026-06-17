using ApplicationLayer.ServicesContracts;
using DomainLayer.Entities;
using System;

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
        private readonly Lazy<IBranchService> _branches;
        private readonly Lazy<IProductService> _products;


        /// <summary>Creates the façade from lazily-resolved service factories.</summary>
        /// <param name="authFactory">Factory that produces the authentication service.</param>
        /// <param name="tenantFactory">Factory that produces the tenant management service.</param>
        public ServiceManager(Func<IAuthService> authFactory, Func<ITenantService> tenantFactory, IBranchService Branches)
        {
            _auth = new Lazy<IAuthService>(authFactory);
            _tenants = new Lazy<ITenantService>(tenantFactory);
            _branches = new Lazy<IBranchService>(Branches);
            _products = new Lazy<IProductService>(Products);     // in the ctor body

        }

        /// <inheritdoc />
        public IAuthService Auth => _auth.Value;

        /// <inheritdoc />
        public ITenantService Tenants => _tenants.Value;

        public IBranchService Branches => _branches.Value;
        public IProductService Products => _products.Value;  // property

    }
}
