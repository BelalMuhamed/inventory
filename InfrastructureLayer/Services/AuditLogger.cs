// InfrastructureLayer/Services/AuditLogger.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using Microsoft.AspNetCore.Http;

namespace InfrastructureLayer.Services
{
    /// <summary>Default <see cref="IAuditLogger"/> writing rows straight to the audit table.</summary>
    public sealed class AuditLogger : IAuditLogger
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>Creates the logger over the shared context.</summary>
        public AuditLogger(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        public async Task LogLoginAsync(string username, bool isSystemAdmin, long? tenantId, CancellationToken cancellationToken = default)
        {
            _context.Set<AuditLog>().Add(new AuditLog
            {
                TenantId = tenantId,
                ActorTenantId = isSystemAdmin ? null : tenantId,
                ActorUsername = username,
                Action = "Login",
                EntityName = isSystemAdmin ? nameof(SystemAdmin) : nameof(Tenant),
                EntityId = tenantId?.ToString() ?? username,
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                Timestamp = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}