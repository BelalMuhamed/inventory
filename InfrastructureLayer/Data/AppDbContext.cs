using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApplicationLayer.Contracts;
using DomainLayer.Common;
using DomainLayer.Entities;
using DomainLayer.Enums;
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
        public DbSet<Product> Products => Set<Product>();        // alongside Branches


        /// <summary>Bootstrap system-administrator accounts.</summary>
        public DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();

        /// <summary>Persisted refresh tokens.</summary>
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        /// <summary>Persisted Audit log.</summary>
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        public DbSet<Branch> Branches => Set<Branch>();
        public DbSet<Stock> Stocks => Set<Stock>();
        public DbSet<ProductItem> Cards => Set<ProductItem>();
        public DbSet<Batch> Batches => Set<Batch>();




        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            ConfigureTenant(modelBuilder);
            ConfigureSystemAdmin(modelBuilder);
            ConfigureRefreshToken(modelBuilder);
            ConfigureBranch(modelBuilder);
            ConfigureProduct(modelBuilder);
            ConfigureStock(modelBuilder);
            ConfigureCards(modelBuilder);
            ConfigureBatches(modelBuilder);

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
            #region tenant scoping filter
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
            #endregion

            #region audit log 
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs");
                entity.HasKey(a => a.Id);

                entity.Property(a => a.ActorUsername).IsRequired().HasMaxLength(100);
                entity.Property(a => a.Action).IsRequired().HasMaxLength(50);
                entity.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
                entity.Property(a => a.EntityId).IsRequired().HasMaxLength(50);
                entity.Property(a => a.IpAddress).HasMaxLength(45);

                entity.HasIndex(a => new { a.TenantId, a.Timestamp });
                entity.HasIndex(a => new { a.EntityName, a.EntityId });
                entity.HasIndex(a => new { a.ActorTenantId, a.Timestamp });
                // No soft-delete filter, no DeletedBy: audit rows are immutable.
            });
            #endregion

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
                entity.Property(r => r.IsSystemAdmin).HasDefaultValue(false);

                entity.HasIndex(r => r.TokenHash).IsUnique();
                entity.HasIndex(r => r.userName);
            });
        }

        private static void ConfigureBranch(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branches");
                entity.HasKey(b => b.Id);

                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.Location).HasMaxLength(500);
                entity.Property(b => b.IsActive).HasDefaultValue(true);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(b => b.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);
                entity.HasIndex(x => x.Name)
               .HasDatabaseName("IX_Category_Name");
                entity.HasIndex(b => b.TenantId);
                // UNIQUE (TenantId, Name) among non-deleted rows (ERD §2.1).
                entity.HasIndex(b => new { b.TenantId, b.Name })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(b => !b.IsDeleted);
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.Name
                })
              .HasDatabaseName("IX_Branch_TenantId_Name");
            });
        }

        private static void ConfigureProduct(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Product>(entity =>
            {
                entity.ToTable("Products");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
                entity.Property(p => p.ActivationStatus).HasConversion<byte>().IsRequired();
                entity.Property(p => p.LowProductThreshold).IsRequired().HasDefaultValue(0);
                entity.Property(p => p.ProductTransactionWay).HasConversion<byte>().IsRequired();
                entity.Property(p => p.UsingPrinterType).HasConversion<byte>().IsRequired();

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => p.TenantId);
                // UNIQUE (TenantId, Name) among non-deleted rows (ERD §2.2).
                entity.HasIndex(p => new { p.TenantId, p.Name })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0");

                entity.HasQueryFilter(p => !p.IsDeleted);
                entity.HasIndex(x => new
                {
                 x.TenantId,
                 x.Name
                })
                .HasDatabaseName("IX_Product_TenantId_Name");
            });
        }
        private static void ConfigureStock(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Stock>(entity =>
            {
                // Composite primary key
                entity.HasKey(e => new { e.TenantId, e.BranchId, e.ProductId });

                // Relationships (if not already inferred)
                entity.HasOne(e => e.Bank)
                      .WithMany()
                      .HasForeignKey(e => e.TenantId)
                      .OnDelete(DeleteBehavior.Restrict); // choose appropriate

                entity.HasOne(e => e.SettledBranch)
                      .WithMany()
                      .HasForeignKey(e => e.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.CardType)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);

                // RowVersion is already configured via [Timestamp]
                // Optionally set UpdatedAt default value in SQL
                entity.Property(e => e.UpdatedAt)
                      .HasDefaultValueSql("GETUTCDATE()");

                // Product-level rollup queries (ERD §3.1) — PK already covers (TenantId, BranchId, ProductId).
                entity.HasIndex(e => new { e.TenantId, e.ProductId })
                      .HasDatabaseName("IX_Stocks_TenantId_ProductId");

                // Invariant guards (ERD §3.1): never let the aggregate go negative.
                entity.ToTable(t =>
                {
                    t.HasCheckConstraint("CK_Stocks_AvailableQuantity_NonNegative", "[AvailableQuantity] >= 0");
                    t.HasCheckConstraint("CK_Stocks_HoldQuantity_NonNegative", "[HoldQuantity] >= 0");
                });
            });
        }
        private static void ConfigureCards(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProductItem>(entity =>
            {
                entity.Property(x => x.MaskedPan).IsRequired().HasMaxLength(32);

                entity.Property(x => x.PanFingerprint)
                    .IsRequired()
                    .HasColumnType("binary(32)")
                    .IsFixedLength();

                entity.HasIndex(u => u.CardHolderName)
            .HasDatabaseName("IX_card_holder_name")
            ;

                entity.HasIndex(u => u.Status)
            .HasDatabaseName("IX_card_status_name")
           ;

                // Item identity per tenant (PAN Storage Redesign). Filtered so a soft-deleted
                // item's PAN can be re-issued.
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.PanFingerprint
                })
                .IsUnique()
                .HasFilter("[IsDeleted] = 0")
                .HasDatabaseName("IX_Cards_TenantId_PanFingerprint");

                // Non-unique covering index for the batch/stock query paths (§4.8).
                entity.HasIndex(x => new
                {
                    x.TenantId,
                    x.ProductId,
                    x.BranchID,
                    x.PanFingerprint
                })
                .HasDatabaseName("IX_Cards_TenantId_ProductId_BranchId_PanFingerprint");

                // BatchId is required: an item always belongs to the batch that introduced it.
                // Deleting a batch cascades and removes its items.
                entity.HasOne(x => x.Batch)
                      .WithMany(b => b.CardsInBatch)
                      .HasForeignKey(x => x.BatchId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
        private static void ConfigureBatches(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Batch>(entity =>
            {
                entity.Property(b => b.Name).IsRequired().HasMaxLength(200);
                entity.Property(b => b.FileMac).IsRequired().HasMaxLength(128);
                entity.Property(b => b.OriginalFileName).IsRequired().HasMaxLength(300);
                entity.Property(b => b.BatchStatus).HasDefaultValue(UploadStatus.Failed);

                entity.HasIndex(u => u.Name)
            .HasDatabaseName("IX_batch_name")
           ;

                entity.HasIndex(u => u.BatchStatus)
            .HasDatabaseName("IX_batch_status")
           ;

                // Duplicate-file guard (§4.8), filtered so a soft-deleted batch doesn't block re-upload.
                entity.HasIndex(b => new { b.UploadedByTenantId, b.FileMac })
                      .IsUnique()
                      .HasFilter("[IsDeleted] = 0")
                      .HasDatabaseName("IX_Batches_UploadedByTenantId_FileMac");

                // Batch list queries, newest first (API §4.8 GET /api/inventory/batches).
                entity.HasIndex(b => new { b.UploadedByTenantId, b.UploadedTime })
                      .HasDatabaseName("IX_Batches_UploadedByTenantId_UploadedTime");
            });
        }
    }
}
