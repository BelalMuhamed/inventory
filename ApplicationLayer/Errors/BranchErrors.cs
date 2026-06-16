using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Stable, localizable <see cref="Error"/> catalogue for the branch module.</summary>
    public static class BranchErrors
    {
        public static Error NotFound(long id) =>
            Error.NotFound("Branch.NotFound", $"No branch was found with id {id}.").WithArg(id.ToString());

        public static Error NameAlreadyExists(string name) =>
            Error.Conflict("Branch.NameAlreadyExists", $"A branch named '{name}' already exists for this tenant.").WithArg(name);

        public static Error AlreadyDeleted(long id) =>
            Error.Conflict("Branch.AlreadyDeleted", $"Branch {id} is already deleted.");

        public static Error NotDeleted(long id) =>
            Error.Conflict("Branch.NotDeleted", $"Branch {id} is not deleted.");

        /// <summary>A system-admin create call did not supply a target tenant (→ 422).</summary>
        public static Error TenantRequired() =>
            Error.Validation("Branch.TenantRequired", "A target tenant id is required when creating a branch as a system admin.");

        /// <summary>The supplied target tenant does not exist (→ 422).</summary>
        public static Error TargetTenantNotFound(long tenantId) =>
            Error.Validation("Branch.TargetTenantNotFound", $"No tenant exists with id {tenantId}.").WithArg(tenantId.ToString());

        /// <summary>The caller's principal could not be resolved (no tenant context / unknown admin) (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Branch.ActorNotResolved", "The acting principal could not be resolved.");
    }
}