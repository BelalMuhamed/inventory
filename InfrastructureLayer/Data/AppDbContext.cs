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

        /// <summary>Card transfers between branches (ERD §4.3, table CardsTransferHistory).</summary>
        public DbSet<CardTransfer> CardTransfers => Set<CardTransfer>();

        /// <summary>Per-product lines on a transfer (ERD §4.4).</summary>
        public DbSet<CardTransferProduct> CardTransferProducts => Set<CardTransferProduct>();

        /// <summary>Individually tracked cards on a Known-way transfer (ERD §4.5).</summary>
        public DbSet<CardTransferItem> CardTransferItems => Set<CardTransferItem>();

        /// <summary>Card write-offs (API §4.10, Addendum A).</summary>
        public DbSet<CardDisposal> CardDisposals => Set<CardDisposal>();

        /// <summary>Cards written off under a disposal (API §4.10, Addendum A).</summary>
        public DbSet<CardDisposalItem> CardDisposalItems => Set<CardDisposalItem>();

        /// <summary>Branch stock requests (ERD §4.1, API §4.9).</summary>
        public DbSet<BranchRequest> BranchRequests => Set<BranchRequest>();

        /// <summary>Requested product lines on a branch request (ERD §4.2, API §4.9).</summary>
        public DbSet<BranchRequestItem> BranchRequestItems => Set<BranchRequestItem>();


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
            ConfigureCardTransfers(modelBuilder);
            ConfigureCardDisposals(modelBuilder);
            ConfigureBranchRequests(modelBuilder);

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

                // BranchID is optional (Transactions §4.10, Q4): null = in transit or unassigned.
                // Declared explicitly rather than left to convention for two reasons: an optional
                // relationship would otherwise default to ClientSetNull, and NoAction is what the
                // ERD (§3.3) specifies — deleting a branch must never delete or silently detach
                // the cards that were sitting at it.
                entity.HasOne(x => x.Branch)
                      .WithMany()
                      .HasForeignKey(x => x.BranchID)
                      .OnDelete(DeleteBehavior.NoAction);

                // Supports the unassigned-pool query (BranchID IS NULL) that the transfer and
                // print paths both need, and the per-branch availability count.
                entity.HasIndex(x => new { x.TenantId, x.BranchID, x.Status })
                      .HasDatabaseName("IX_Cards_TenantId_BranchId_Status");
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

        /// <summary>
        /// Configures the transfer aggregate (ERD §4.3–§4.5, API §4.10).
        /// <para>
        /// Every foreign key out of this aggregate is <c>NoAction</c> except the two that point at
        /// the transfer header itself. That is not caution for its own sake: <c>SourceBranchId</c>
        /// and <c>TargetBranchId</c> both reference <c>Branches</c>, so anything other than
        /// <c>NoAction</c> is a guaranteed multiple-cascade-path error at migration time. It is
        /// also correct on the merits — deleting a branch or a product must never silently erase
        /// the movement history that references it.
        /// </para>
        /// <para>
        /// No soft-delete query filter and no <c>AuditableEntity</c>: these tables are append-only
        /// (ERD §6.5).
        /// </para>
        /// </summary>
        private static void ConfigureCardTransfers(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CardTransfer>(entity =>
            {
                // Name and check constraint in one ToTable call: a second, name-less ToTable
                // overload would reconfigure the table mapping rather than add to it.
                entity.ToTable("CardsTransferHistory", t => t.HasCheckConstraint(   // ERD §4.3 table name
                    "CK_CardsTransferHistory_SourceNotTarget",
                    "[SourceBranchId] <> [TargetBranchId]"));
                entity.HasKey(t => t.Id);

                entity.Property(t => t.TransactionStatus).HasConversion<byte>().IsRequired();
                entity.Property(t => t.Origin).HasConversion<byte>().IsRequired();
                entity.Property(t => t.ActionNotes).HasMaxLength(500);

                entity.HasOne(t => t.Tenant)
                      .WithMany()
                      .HasForeignKey(t => t.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.CreatedByTenant)
                      .WithMany()
                      .HasForeignKey(t => t.CreatedByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.SourceBranch)
                      .WithMany()
                      .HasForeignKey(t => t.SourceBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(t => t.TargetBranch)
                      .WithMany()
                      .HasForeignKey(t => t.TargetBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                // API §4.9: the branch request this transfer fulfils, or null for a direct
                // transfer. Every existing row is NULL — the column already existed (decision Q2
                // of the original Transfers workstream); this adds only the constraint and
                // navigation, so no data migration is needed.
                entity.HasOne(t => t.BranchRequest)
                      .WithMany()
                      .HasForeignKey(t => t.BranchRequestId)
                      .OnDelete(DeleteBehavior.NoAction);

                // Self-reference: an auto-generated return points back at the transfer whose
                // partial receipt produced it.
                entity.HasOne(t => t.ParentTransfer)
                      .WithMany()
                      .HasForeignKey(t => t.ParentTransferId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ERD §4.3 index set, plus two for the origin/parent queries added by decision Q5.
                entity.HasIndex(t => new { t.TenantId, t.CreatedAt })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_CreatedAt");
                entity.HasIndex(t => new { t.TenantId, t.BranchRequestId })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_BranchRequestId");
                entity.HasIndex(t => t.SourceBranchId)
                      .HasDatabaseName("IX_CardsTransferHistory_SourceBranchId");
                entity.HasIndex(t => t.TargetBranchId)
                      .HasDatabaseName("IX_CardsTransferHistory_TargetBranchId");
                entity.HasIndex(t => new { t.TenantId, t.TransactionStatus })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_TransactionStatus");
                entity.HasIndex(t => new { t.TenantId, t.Origin })
                      .HasDatabaseName("IX_CardsTransferHistory_TenantId_Origin");
                entity.HasIndex(t => t.ParentTransferId)
                      .HasDatabaseName("IX_CardsTransferHistory_ParentTransferId");
            });

            modelBuilder.Entity<CardTransferProduct>(entity =>
            {
                entity.HasKey(p => p.Id);

                entity.Property(p => p.ProductTransactionWay).HasConversion<byte>().IsRequired();

                entity.HasOne(p => p.CardTransfer)
                      .WithMany(t => t.Products)
                      .HasForeignKey(p => p.CardTransferId)
                      .OnDelete(DeleteBehavior.Cascade);   // lines belong to the transfer (ERD §4.4)

                entity.HasOne(p => p.Product)
                      .WithMany()
                      .HasForeignKey(p => p.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(p => p.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(p => new { p.TenantId, p.CardTransferId })
                      .HasDatabaseName("IX_CardTransferProducts_TenantId_CardTransferId");
                entity.HasIndex(p => new { p.CardTransferId, p.ProductId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardTransferProducts_CardTransferId_ProductId");

                entity.ToTable("CardTransferProducts", t =>
                {
                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_TransactedQuantity_Positive",
                        "[TransactedQuantity] > 0");

                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_RealQuantityReceived_NonNegative",
                        "[RealQuantityReceived] IS NULL OR [RealQuantityReceived] >= 0");

                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_DisposedQuantity_NonNegative",
                        "[DisposedQuantity] IS NULL OR [DisposedQuantity] >= 0");

                    // The settlement identity (Addendum A §2.3): what was received plus what was
                    // written off can never exceed what was sent. The returned remainder is
                    // whatever is left over, so this one constraint keeps all three honest and
                    // makes an arithmetic slip in the service layer fail loudly at the database
                    // rather than quietly manufacturing stock.
                    t.HasCheckConstraint(
                        "CK_CardTransferProducts_SettlementWithinTransacted",
                        "ISNULL([RealQuantityReceived], 0) + ISNULL([DisposedQuantity], 0) <= [TransactedQuantity]");
                });
            });

            modelBuilder.Entity<CardTransferItem>(entity =>
            {
                entity.ToTable("CardTransferItems");
                entity.HasKey(i => i.Id);

                entity.Property(i => i.ReceiveStatus).HasConversion<byte>().IsRequired();

                entity.HasOne(i => i.CardTransfer)
                      .WithMany(t => t.Items)
                      .HasForeignKey(i => i.CardTransferId)
                      .OnDelete(DeleteBehavior.Cascade);   // items belong to the transfer (ERD §4.5)

                // NoAction, which also means a batch cannot be deleted while its cards are in
                // flight: Batch → ProductItem cascades, and this constraint blocks that cascade.
                // That is the intended behaviour, not an accident of configuration.
                entity.HasOne(i => i.ProductItem)
                      .WithMany()
                      .HasForeignKey(i => i.ProductItemId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.CardTransferId })
                      .HasDatabaseName("IX_CardTransferItems_TenantId_CardTransferId");
                entity.HasIndex(i => i.ProductItemId)
                      .HasDatabaseName("IX_CardTransferItems_ProductItemId");
                entity.HasIndex(i => new { i.CardTransferId, i.ProductItemId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardTransferItems_CardTransferId_ProductItemId");
            });
        }

        /// <summary>
        /// Configures the disposal aggregate (API §4.10, Addendum A). Append-only, same rationale
        /// as transfers: no audit block, no soft delete, no query filter.
        /// </summary>
        private static void ConfigureCardDisposals(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<CardDisposal>(entity =>
            {
                entity.ToTable("CardDisposals");
                entity.HasKey(d => d.Id);

                // Required at the database level too, not only in validation: a write-off with no
                // stated reason is the exact scenario this table exists to make impossible.
                entity.Property(d => d.Reason).IsRequired().HasMaxLength(500);

                entity.HasOne(d => d.Tenant)
                      .WithMany()
                      .HasForeignKey(d => d.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.DisposedByTenant)
                      .WithMany()
                      .HasForeignKey(d => d.DisposedByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.Branch)
                      .WithMany()
                      .HasForeignKey(d => d.BranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(d => d.CardTransfer)
                      .WithMany()
                      .HasForeignKey(d => d.CardTransferId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(d => new { d.TenantId, d.DisposedAt })
                      .HasDatabaseName("IX_CardDisposals_TenantId_DisposedAt");
                entity.HasIndex(d => new { d.TenantId, d.BranchId })
                      .HasDatabaseName("IX_CardDisposals_TenantId_BranchId");
                entity.HasIndex(d => d.CardTransferId)
                      .HasDatabaseName("IX_CardDisposals_CardTransferId");
            });

            modelBuilder.Entity<CardDisposalItem>(entity =>
            {
                entity.ToTable("CardDisposalItems");
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.CardDisposal)
                      .WithMany(d => d.Items)
                      .HasForeignKey(i => i.CardDisposalId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.ProductItem)
                      .WithMany()
                      .HasForeignKey(i => i.ProductItemId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.CardDisposalId })
                      .HasDatabaseName("IX_CardDisposalItems_TenantId_CardDisposalId");

                // A card can only ever be written off once, so this unique index is a safety net
                // rather than a real constraint on the domain — the service layer refuses to
                // dispose an already-disposed card long before this fires.
                entity.HasIndex(i => new { i.CardDisposalId, i.ProductItemId })
                      .IsUnique()
                      .HasDatabaseName("UX_CardDisposalItems_CardDisposalId_ProductItemId");
            });
        }

        /// <summary>
        /// Configures branch stock requests and their lines (ERD §4.1–§4.2, tables
        /// <c>BranchRequests</c> / <c>BranchRequestItems</c>; API §4.9).
        /// <para>
        /// <c>BranchRequest</c> does not derive from <c>AuditableEntity</c> (decision Q-09): no
        /// soft delete, no query filter, no restore endpoint — the same append-only-with-status
        /// shape <see cref="ConfigureCardTransfers"/> already established for
        /// <see cref="CardTransfer"/>.
        /// </para>
        /// <para>
        /// Cascade-path check: <see cref="BranchRequestItem"/> has exactly one cascade parent
        /// (<c>RequestId</c> → <c>BranchRequests</c>) and two NoAction parents (<c>ProductId</c>,
        /// the denormalized <c>TenantId</c>) — the same shape as <see cref="CardTransferProduct"/>.
        /// <see cref="BranchRequest"/> itself has zero cascade edges into it (every FK out of it
        /// is NoAction), so there is no multiple-cascade-path risk anywhere in this aggregate.
        /// </para>
        /// </summary>
        private static void ConfigureBranchRequests(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BranchRequest>(entity =>
            {
                entity.ToTable("BranchRequests");   // ERD §4.1 table name
                entity.HasKey(r => r.Id);

                entity.Property(r => r.RequestStatus).HasConversion<byte>().IsRequired();
                entity.Property(r => r.ActionNotes).HasMaxLength(500);

                // Two distinct relationships to Tenants — both need their own navigation, or EF
                // Core silently reconfigures the same one and leaves the second FK unmapped (the
                // same pitfall CardTransfer's Tenant/CreatedByTenant pair already documents).
                entity.HasOne(r => r.Tenant)
                      .WithMany()
                      .HasForeignKey(r => r.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.ActionTakenByTenant)
                      .WithMany()
                      .HasForeignKey(r => r.ActionTakenByTenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne(r => r.RequestingBranch)
                      .WithMany()
                      .HasForeignKey(r => r.RequestingBranchId)
                      .OnDelete(DeleteBehavior.NoAction);

                // ERD §4.1 index set.
                entity.HasIndex(r => new { r.TenantId, r.RequestStatus })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestStatus");
                entity.HasIndex(r => new { r.TenantId, r.RequestingBranchId })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestingBranchId");
                entity.HasIndex(r => new { r.TenantId, r.RequestDateTime })
                      .HasDatabaseName("IX_BranchRequests_TenantId_RequestDateTime");
            });

            modelBuilder.Entity<BranchRequestItem>(entity =>
            {
                entity.HasKey(i => i.Id);

                entity.HasOne(i => i.Request)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.RequestId)
                      .OnDelete(DeleteBehavior.Cascade);   // lines belong to the request (ERD §4.2)

                entity.HasOne(i => i.Product)
                      .WithMany()
                      .HasForeignKey(i => i.ProductId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasOne<Tenant>()
                      .WithMany()
                      .HasForeignKey(i => i.TenantId)
                      .OnDelete(DeleteBehavior.NoAction);

                entity.HasIndex(i => new { i.TenantId, i.RequestId })
                      .HasDatabaseName("IX_BranchRequestItems_TenantId_RequestId");
                entity.HasIndex(i => new { i.RequestId, i.ProductId })
                      .IsUnique()
                      .HasDatabaseName("UX_BranchRequestItems_RequestId_ProductId");

                entity.ToTable("BranchRequestItems", t =>
                {
                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_AskedQuantity_Positive",
                        "[AskedQuantity] > 0");

                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_DispatchedQuantity_NonNegative",
                        "[DispatchedQuantity] >= 0");

                    t.HasCheckConstraint(
                        "CK_BranchRequestItems_ReceivedQuantity_NonNegative",
                        "[ReceivedQuantity] >= 0");
                });
            });
        }
    }
}
