using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ApplicationLayer.Contracts;
using ApplicationLayer.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
// InfrastructureLayer/Security/JwtTokenGenerator.cs  (claim assembly — key parts)
namespace InfrastructureLayer.Security
{
    public sealed class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly JwtOptions _jwtOptions;

        public JwtTokenGenerator(IOptions<JwtOptions> jwtOptions)
        {
            _jwtOptions = jwtOptions?.Value ?? throw new ArgumentNullException(nameof(jwtOptions));
        }

        /// <summary>Claim carrying the principal's username (tenant or system admin).</summary>
        public const string UsernameClaim = "username";

        /// <summary>Claim flagging a system-admin token.</summary>
        public const string IsSystemAdminClaim = "isSystemAdmin";
        
        /// <inheritdoc />
        public AccessToken CreateForTenant(string username) =>
            CreateToken(username, isSystemAdmin: false);

        /// <inheritdoc />
        public AccessToken CreateForSystemAdmin(string username) =>
            CreateToken(username, isSystemAdmin: true);

        private AccessToken CreateToken(string username, bool isSystemAdmin)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

            var claims = new[]
            {
                new Claim(UsernameClaim, username),
                new Claim(IsSystemAdminClaim, isSystemAdmin ? "true" : "false"),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            string jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return new AccessToken(jwt, expiresAt);
        }
    }
}