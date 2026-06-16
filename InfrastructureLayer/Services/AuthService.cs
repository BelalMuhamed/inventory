using ApplicationLayer.Contracts;
using ApplicationLayer.DTOs.Auth;
using ApplicationLayer.Errors;
using ApplicationLayer.Options;
using ApplicationLayer.ServicesContracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly IAuditLogger _auditLogger;
        private readonly JwtOptions _jwtOptions;

        /// <summary>Creates the service with its collaborators (constructor injection only).</summary>
        public AuthService(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator
          ,
            IOptions<JwtOptions> jwtOptions,IAuditLogger auditLogger)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
           
            _auditLogger = auditLogger;
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
                return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials());
            }

          
            AccessToken access = _tokenGenerator.CreateForTenant(tenant.Username, tenant.Id);
            AuthResponse response = await IssueWithRefreshAsync(
                access, request.Username, isSystemAdmin: false, tenantId: tenant.Id, cancellationToken);
            await _auditLogger.LogLoginAsync(tenant.Username, isSystemAdmin: false, tenantId: tenant.Id, cancellationToken);

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
                return Result.Failure<AuthResponse>(AuthErrors.InvalidCredentials());
            }

            AccessToken access = _tokenGenerator.CreateForSystemAdmin(admin.Username);
            AuthResponse response = await IssueWithRefreshAsync(
     access, request.Username, isSystemAdmin: true, tenantId: null, cancellationToken);

            await _auditLogger.LogLoginAsync(admin.Username, isSystemAdmin: true, tenantId: null, cancellationToken);


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
                return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken());
            }

            // A tenant token minted before this column existed has no TenantId; force a fresh login
            // rather than mint a token without the tenantId claim (same posture as the IsSystemAdmin fix).
            if (!stored.IsSystemAdmin && stored.TenantId is null)
            {
                return Result.Failure<AuthResponse>(AuthErrors.InvalidRefreshToken());
            }

            AccessToken access = stored.IsSystemAdmin
                ? _tokenGenerator.CreateForSystemAdmin(stored.userName)
                : _tokenGenerator.CreateForTenant(stored.userName, stored.TenantId!.Value);

            (RefreshToken successor, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(stored.userName, stored.IsSystemAdmin, stored.TenantId);

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
      AccessToken access, string userName, bool isSystemAdmin, long? tenantId, CancellationToken cancellationToken)
        {
            (RefreshToken token, string rawRefresh, DateTime refreshExpiry) =
                BuildRefreshToken(userName, isSystemAdmin, tenantId);

            await _unitOfWork.RefreshTokens.AddAsync(token, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return new AuthResponse(access.Token, access.ExpiresAt, rawRefresh, refreshExpiry);
        }

        private (RefreshToken Entity, string Raw, DateTime ExpiresAt) BuildRefreshToken(
     string userName, bool isSystemAdmin, long? tenantId)
        {
            string raw = GenerateOpaqueToken();
            DateTime now = DateTime.UtcNow;
            DateTime expiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

            var entity = new RefreshToken
            {
                userName = userName,
                IsSystemAdmin = isSystemAdmin,
                TenantId = tenantId,
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

      
    }
}
