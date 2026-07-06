// ApplicationLayer/Errors/AuthErrors.cs
using DomainLayer.Common;

namespace ApplicationLayer.Errors
{
    /// <summary>Auth error catalogue. English defaults here; localized centrally by code.</summary>
    public static class AuthErrors
    {
        /// <summary>Username/password mismatch or inactive principal (→ 401).</summary>
        public static Error InvalidCredentials() =>
            Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");

        /// <summary>Refresh token unknown, expired, or revoked (→ 401).</summary>
        public static Error InvalidRefreshToken() =>
            Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired.");
        /// <summary>The caller's principal could not be resolved (no tenant context / unknown admin) (→ 401).</summary>
        public static Error ActorNotResolved() =>
            Error.Unauthorized("Product.ActorNotResolved", "The acting principal could not be resolved.");


    }
}