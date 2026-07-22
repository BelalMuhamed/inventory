using ApplicationLayer.ServicesContracts;
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
        private readonly Lazy<IStockService> _stocks;
        private readonly Lazy<IProductItemService> _productItems;

        public ServiceManager(
            Func<IAuthService> authFactory,
            Func<ITenantService> tenantFactory,
            Func<IBranchService> branchFactory,
            Func<IProductService> productFactory,
            Func<IStockService> stockFactory,
            Func<IProductItemService> productItemFactory)
        {
            _auth = new Lazy<IAuthService>(authFactory);
            _tenants = new Lazy<ITenantService>(tenantFactory);
            _branches = new Lazy<IBranchService>(branchFactory);
            _products = new Lazy<IProductService>(productFactory);
            _stocks = new Lazy<IStockService>(stockFactory);
            _productItems = new Lazy<IProductItemService>(productItemFactory);
        }

        public IAuthService Auth => _auth.Value;
        public ITenantService Tenants => _tenants.Value;
        public IBranchService Branches => _branches.Value;
        public IProductService Products => _products.Value;
        public IStockService Stocks => _stocks.Value;
        public IProductItemService ProductItems => _productItems.Value;
    }
}