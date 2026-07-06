using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the product module.</summary>
    public static class ProductErrors
    {
        public static Error NotFound(long id) =>
            Error.NotFound("Product.NotFound", $"No product was found with id {id}.").WithArg(id.ToString());

        public static Error NameAlreadyExists(string name) =>
            Error.Conflict("Product.NameAlreadyExists", $"A product named '{name}' already exists for this tenant.").WithArg(name);

        public static Error AlreadyDeleted(long id) =>
            Error.Conflict("Product.AlreadyDeleted", $"Product {id} is already deleted.");

        public static Error NotDeleted(long id) =>
            Error.Conflict("Product.NotDeleted", $"Product {id} is not deleted.");

        /// <summary>A system-admin create call did not supply a target tenant (→ 422).</summary>
        public static Error TenantRequired() =>
            Error.Validation("Product.TenantRequired", "A target tenant id is required when creating a product as a system admin.");

        /// <summary>The supplied target tenant does not exist (→ 422).</summary>
        public static Error TargetTenantNotFound(long tenantId) =>
            Error.Validation("Product.TargetTenantNotFound", $"No tenant exists with id {tenantId}.").WithArg(tenantId.ToString());

       

        // NOTE (stock seam): when Stock/Transactions exist, add Product.HasStock / Product.HasOpenTransactions
        // (Conflict → 409) to enforce the API §4.6 delete-guard "blocked if open transactions or non-zero stock".
    }
}
