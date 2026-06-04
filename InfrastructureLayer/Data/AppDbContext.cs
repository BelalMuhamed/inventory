using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using Microsoft.EntityFrameworkCore;

namespace InfrastructureLayer.Data
{
    /// <summary>
    /// EF Core unit-of-work boundary and model owner. Holds the auth aggregates introduced so
    /// far and applies cross-cutting model rules in <see cref="OnModelCreating"/>:
    /// per-ERD indexes, soft-delete query filters, and the tenant scoping filter.
    /// <para>
    /// The tenant scoping filter uses <see cref="ICurrentTenant"/> so every query on a
    /// tenant-owned table is constrained to the caller's tenant, except for the system admin,
    /// who bypasses it.
    /// </para>
    /// </summary>
    public sealed class AppDbContext : DbContext
    {
        private readonly ICurrentTenant _currentTenant;

        /// <summary>Creates the context with EF options and the ambient principal accessor.</summary>
        /// <param name="options">EF Core options (provider, connection string).</param>
        /// <param name="currentTenant">Accessor for the authenticated principal's claims.</param>
        public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentTenant currentTenant)
            : base(options)
        {
            _currentTenant = currentTenant;
        }

        /// <summary>Tenant accounts (the authentication identity).</summary>
        public DbSet<Tenant> Tenants => Set<Tenant>();

        /// <summary>Bootstrap system-administrator accounts.</summary>
        public DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();

        /// <summary>Persisted refresh tokens.</summary>
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureTenant(modelBuilder);
            ConfigureSystemAdmin(modelBuilder);
            ConfigureRefreshToken(modelBuilder);
        }

        /// <inheritdoc />
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            StampAuditFields();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void StampAuditFields()
        {
            DateTime utcNow = DateTime.UtcNow;

            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedAt = utcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.UpdatedAt = utcNow;
                        break;
                }
            }
        }

        // InfrastructureLayer/Data/AppDbContext.cs  (ConfigureTenant body — replace)
        private static void ConfigureTenant(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.ToTable("Tenants");
                entity.HasKey(t => t.Id);

                entity.Property(t => t.Code).IsRequired().HasMaxLength(50);
                entity.Property(t => t.Username).IsRequired().HasMaxLength(100);
                entity.Property(t => t.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(t => t.IsActive).HasDefaultValue(true);

                // Code and Username are unique across ALL tenants (including soft-deleted), so a
                // deleted tenant's identifiers stay reserved. IsDeleted is indexed (filtered) to
                // keep the common "active only" queries cheap.
                entity.HasIndex(t => t.Code).IsUnique();
                entity.HasIndex(t => t.Username).IsUnique();
                entity.HasIndex(t => t.IsDeleted).HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(t => !t.IsDeleted);
            });
        }

        private static void ConfigureSystemAdmin(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SystemAdmin>(entity =>
            {
                entity.ToTable("SystemAdmins");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.Username).IsRequired().HasMaxLength(100);
                entity.Property(a => a.PasswordHash).IsRequired().HasMaxLength(256);
                entity.Property(a => a.IsActive).HasDefaultValue(true);

                entity.HasIndex(a => a.Username)
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(a => !a.IsDeleted);
            });
        }

        private static void ConfigureRefreshToken(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.ToTable("RefreshTokens");
                entity.HasKey(r => r.Id);

                entity.Property(r => r.TokenHash).IsRequired().HasMaxLength(128);
                entity.Property(r => r.ReplacedByTokenHash).HasMaxLength(128);

                entity.HasIndex(r => r.TokenHash).IsUnique();
                entity.HasIndex(r => r.TenantId);
                entity.HasIndex(r => r.SystemAdminId);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(r => r.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<SystemAdmin>()
                      .WithMany()
                      .HasForeignKey(r => r.SystemAdminId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Exactly one owner: either a tenant or a system admin, never both, never neither.
                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_RefreshTokens_SingleOwner",
                    "([TenantId] IS NOT NULL AND [SystemAdminId] IS NULL) OR ([TenantId] IS NULL AND [SystemAdminId] IS NOT NULL)"));
            });
        }
    }
}
