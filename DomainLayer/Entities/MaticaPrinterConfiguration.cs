using DomainLayer.Common;

namespace DomainLayer.Entities
{
    /// <summary>
    /// Matica-specific machine configuration (ERD §6.2, table
    /// <c>MaticaPrinterConfigurations</c>; Printing Module decision Q-01). A 1:1 extension of
    /// <see cref="Printer"/> — every Matica printer has exactly one row here, keyed by
    /// <see cref="PrinterId"/>; Evolis printers have none, since Evolis requires no server-side
    /// machine configuration (module requirement §1).
    /// <para>
    /// Belongs to the same aggregate as its owning <see cref="Printer"/> (not a cross-aggregate
    /// reference) — deleting the printer removes this row too.
    /// </para>
    /// </summary>
    public sealed class MaticaPrinterConfiguration : AuditableEntity
    {
        /// <summary>Primary key (BIGINT IDENTITY).</summary>
        public long Id { get; set; }

        /// <summary>Owning printer id (FK → Printers.Id). Unique — enforces the 1:1 relationship.</summary>
        public long PrinterId { get; set; }

        /// <summary>
        /// Navigation to the owning printer. Added in P5: <c>PrinterConfigurationService</c>
        /// inserts a new printer and its Matica configuration in the same
        /// <c>ExecuteInTransactionAsync</c> call, so <see cref="PrinterId"/> does not exist yet
        /// when the configuration object is built — EF Core needs this navigation to fix up the
        /// foreign key from the printer's generated identity once the single <c>SaveChanges</c>
        /// call actually runs. Set <see cref="Printer"/> (not <see cref="PrinterId"/> directly)
        /// when creating a new Matica printer; the reverse is fine once the printer already has a
        /// real id. Metadata only: no new column, no migration required beyond what P1 already
        /// produced.
        /// </summary>
        public Printer Printer { get; set; } = null!;

        /// <summary>Matica feeder identifier.</summary>
        public int FeederId { get; set; }

        /// <summary>Matica hopper identifier.</summary>
        public int HopperId { get; set; }

        /// <summary>Matica reject-bin identifier.</summary>
        public int RejectedId { get; set; }

        /// <summary>Communication port (e.g. COM port or TCP port) the printer listens on.</summary>
        public string Port { get; set; } = string.Empty;
    }
}
