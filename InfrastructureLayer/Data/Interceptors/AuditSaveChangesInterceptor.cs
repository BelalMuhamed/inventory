// InfrastructureLayer/Data/Interceptors/AuditSaveChangesInterceptor.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InfrastructureLayer.Data.Interceptors
{
    /// <summary>
    /// Emits immutable <see cref="AuditLog"/> rows for inserts/updates/soft-deletes of
    /// <see cref="AuditableEntity"/> types (ERD §5.1, API §3.4). Non-CRUD actions such as Login
    /// are logged separately via a service hook. Runs inside the caller's transaction.
    /// </summary>
    public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
    {
        private readonly ICurrentTenant _currentTenant;
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>Creates the interceptor with the current-principal and HTTP accessors.</summary>
        public AuditSaveChangesInterceptor(ICurrentTenant currentTenant, IHttpContextAccessor httpContextAccessor)
        {
            _currentTenant = currentTenant;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            DbContext? context = eventData.Context;
            if (context is not null)
            {
                AddAuditEntries(context);
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private void AddAuditEntries(DbContext context)
        {
            string actor = _currentTenant.Username ?? "system";
            string? ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
            DateTime now = DateTime.UtcNow;

            // Snapshot changed auditable entries first; adding AuditLogs mutates the change tracker.
            List<(EntityEntry Entry, string Action)> targets = context.ChangeTracker
                .Entries<AuditableEntity>()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => ((EntityEntry)e, ResolveAction(e)))
                .ToList();

            foreach ((EntityEntry entry, string action) in targets)
            {
                context.Set<AuditLog>().Add(new AuditLog
                {
                    TenantId = _currentTenant.IsSystemAdmin ? null : null, // owning tenant set per-entity when tenant-owned tables exist
                    ActorTenantId = _currentTenant.IsSystemAdmin ? null : null,
                    ActorUsername = actor,
                    Action = action,
                    EntityName = entry.Entity.GetType().Name,
                    EntityId = TryGetKey(entry),
                    OldValue = action == "Created" ? null : Serialize(entry.OriginalValues),
                    NewValue = action == "Deleted" ? null : Serialize(entry.CurrentValues),
                    IpAddress = ip,
                    Timestamp = now
                });
            }
        }

        private static string ResolveAction(EntityEntry entry) => entry.State switch
        {
            EntityState.Added => "Created",
            EntityState.Deleted => "HardDeleted",
            // A soft delete is a Modified row flipping IsDeleted to true.
            EntityState.Modified when entry.Entity is AuditableEntity { IsDeleted: true } &&
                entry.Property(nameof(AuditableEntity.IsDeleted)).IsModified => "Deleted",
            _ => "Updated"
        };

        private static string TryGetKey(EntityEntry entry)
        {
            object? key = entry.Metadata.FindPrimaryKey()?.Properties
                .Select(p => entry.Property(p.Name).CurrentValue)
                .FirstOrDefault();
            return key?.ToString() ?? string.Empty;
        }

        private static string Serialize(PropertyValues values)
        {
            Dictionary<string, object?> dict = values.Properties
                .ToDictionary(p => p.Name, p => values[p.Name]);
            return JsonSerializer.Serialize(dict);
        }
    }
}