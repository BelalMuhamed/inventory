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

    }
}