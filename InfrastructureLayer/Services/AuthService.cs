using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace InfrastructureLayer.Services
{
    /// <summary>
    /// Authentication service implementing tenant/admin login, refresh-token rotation, and
    /// logout. Credential and token failures are returned as
    /// <see cref="ErrorCategory.Unauthorized"/> results, not exceptions. User-facing messages are
    /// localized via <see cref="IStringLocalizer"/> (Messages_en/Messages_ar).
    /// </summary>
    public sealed class AuthService : IAuthService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly IStringLocalizer<AuthService> _localizer;
        private readonly JwtOptions _jwtOptions;

        /// <summary>Creates the service with its collaborators (constructor injection only).</summary>
        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator,
            IStringLocalizer<AuthService> localizer,
            IOptions<JwtOptions> jwtOptions)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _localizer = localizer;
            _jwtOptions = jwtOptions.Value;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> LoginTenantAsync(
            TenantLoginRequest request, CancellationToken cancellationToken = default)
        {
            Tenant? tenant = await _unitOfWork.Tenants
                .GetActiveByUsernameAsync(request.Username, cancellationToken);

            if (tenant is null || !_passwordHasher.Verify(tenant.PasswordHash, request.Password))
            {
                return Result.Failure<AuthResponse>(InvalidCredentials());
            }

            AccessToken access = _tokenGenerator.CreateForTenant(tenant.Id);
            AuthResponse response = await IssueWithRefreshAsync(
                access, tenantId: tenant.Id, systemAdminId: null, cancellationToken);

            // Audit hook (Login) is raised by the SaveChanges interceptor / service hook in the
            // cross-cutting milestone; the refresh-token insert above provides its commit point.
            return response;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> LoginSystemAdminAsync(
            AdminLoginRequest request, CancellationToken cancellationToken = default)
        {
            SystemAdmin? admin = await _unitOfWork.SystemAdmins
                .GetActiveByUsernameAsync(request.Username, cancellationToken);

            if (admin is null || !_passwordHasher.Verify(admin.PasswordHash, request.Password))
            {
                return Result.Failure<AuthResponse>(InvalidCredentials());
            }

            AccessToken access = _tokenGenerator.CreateForSystemAdmin(admin.Id);
            AuthResponse response = await IssueWithRefreshAsync(
                access, tenantId: null, systemAdminId: admin.Id, cancellationToken);

            return response;
        }

        /// <inheritdoc />
        public async Task<Result<AuthResponse>> RefreshAsync(
            RefreshRequest request, CancellationToken cancellationToken = default)
        {
            string presentedHash = HashToken(request.RefreshToken);
            RefreshToken? stored = await _unitOfWork.RefreshTokens
                .GetByHashAsync(presentedHash, cancellationToken);

            if (stored is null || !stored.IsActive(DateTime.UtcNow))
            {
                return Result.Failure<AuthResponse>(InvalidRefreshToken());
            }

            // Rotate: revoke the presented token, then mint a fresh access/refresh pair for the
            // same principal. Both writes commit together in one unit of work.
            AccessToken access = stored.TenantId is not null
                ? _tokenGenerator.CreateForTenant(stored.TenantId.Value)
                : _tokenGenerator.CreateForSystemAdmin(stored.SystemAdminId!.Value);

            (RefreshToken successor, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(stored.TenantId, stored.SystemAdminId);

            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByTokenHash = successor.TokenHash;
            _unitOfWork.RefreshTokens.Update(stored);
            await _unitOfWork.RefreshTokens.AddAsync(successor, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, refreshExpiry);
        }

        /// <inheritdoc />
        public async Task<Result> LogoutAsync(
            LogoutRequest request, CancellationToken cancellationToken = default)
        {
            string presentedHash = HashToken(request.RefreshToken);
            RefreshToken? stored = await _unitOfWork.RefreshTokens
                .GetByHashAsync(presentedHash, cancellationToken);

            // Idempotent: revoke if live; otherwise succeed silently so logout cannot be used to
            // probe whether a token exists.
            if (stored is not null && stored.RevokedAt is null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                _unitOfWork.RefreshTokens.Update(stored);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        private async Task<AuthResponse> IssueWithRefreshAsync(
            AccessToken access, long? tenantId, long? systemAdminId, CancellationToken cancellationToken)
        {
            (RefreshToken token, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(tenantId, systemAdminId);

            await _unitOfWork.RefreshTokens.AddAsync(token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, refreshExpiry);
        }

        private (RefreshToken Entity, string Raw, DateTime ExpiresAt) BuildRefreshToken(
            long? tenantId, long? systemAdminId)
        {
            string raw = GenerateOpaqueToken();
            DateTime now = DateTime.UtcNow;
            DateTime expiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

            var entity = new RefreshToken
            {
                TenantId = tenantId,
                SystemAdminId = systemAdminId,
                TokenHash = HashToken(raw),
                CreatedAt = now,
                ExpiresAt = expiresAt
            };

            return (entity, raw, expiresAt);
        }

        private static string GenerateOpaqueToken()
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes);
        }

        private static string HashToken(string rawToken)
        {
            byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(hash);
        }

        private Error InvalidCredentials() =>
            Error.Unauthorized("Auth.InvalidCredentials", _localizer["Auth.InvalidCredentials"]);

        private Error InvalidRefreshToken() =>
            Error.Unauthorized("Auth.InvalidRefreshToken", _localizer["Auth.InvalidRefreshToken"]);
    }
}
