using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace InfrastructureLayer.Security
{
    /// <summary>
    /// Issues HMAC-SHA256-signed JWTs. Per API Spec §2 the claim set is intentionally minimal:
    /// tenant tokens carry <c>sub</c> and <c>tenantId</c>; the admin token adds
    /// <c>isSystemAdmin</c>. No role, permissions, or branch claims are emitted.
    /// </summary>
    public sealed class JwtTokenGenerator : IJwtTokenGenerator
    {
        /// <summary>Claim type carrying the authenticated tenant's id.</summary>
        public const string TenantIdClaim = "tenantId";

        /// <summary>Claim type marking a system-admin token.</summary>
        public const string IsSystemAdminClaim = "isSystemAdmin";

        /// <summary>Constant subject used for tenant tokens, per the ERD JWT notes.</summary>
        private const string TenantSubject = "tenant_account";

        private readonly JwtOptions _options;

        /// <summary>Creates the generator from bound JWT options.</summary>
        /// <param name="options">The configured <see cref="JwtOptions"/>.</param>
        public JwtTokenGenerator(IOptions<JwtOptions> options)
        {
            _options = options.Value;
        }

        /// <inheritdoc />
        public AccessToken CreateForTenant(long tenantId)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, TenantSubject),
                new(TenantIdClaim, tenantId.ToString())
            };
            return Create(claims);
        }

        /// <inheritdoc />
        public AccessToken CreateForSystemAdmin(long systemAdminId)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, systemAdminId.ToString()),
                new(IsSystemAdminClaim, "true")
            };
            return Create(claims);
        }

        private AccessToken Create(IEnumerable<Claim> claims)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: credentials);

            string serialized = new JwtSecurityTokenHandler().WriteToken(token);
            return new AccessToken(serialized, expiresAt);
        }
    }
}
