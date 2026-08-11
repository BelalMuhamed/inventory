using System;
using ApplicationLayer.DTOs.Products;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.Printing
{
    // =====================================================================================
    //  Printers (ERD §6, Printing Module Q-01/Q-09)
    // =====================================================================================

    /// <summary>Matica-only machine configuration, nested under a printer payload (ERD §6.2).</summary>
    public sealed record MaticaPrinterConfigRequest(int FeederId, int HopperId, int RejectedId, string Port);

    /// <summary>Matica-only machine configuration as returned to clients (ERD §6.2).</summary>
    public sealed record MaticaPrinterConfigResponse(int FeederId, int HopperId, int RejectedId, string Port);

    /// <summary>
    /// Create payload for <c>POST /api/printers</c> (Printing Module Q-01/Q-09: system-admin
    /// only). <paramref name="MaticaConfig"/> is required when <paramref name="UsingPrinterType"/>
    /// is <see cref="UsingPrinterType.Matica"/> and must be absent when it is
    /// <see cref="UsingPrinterType.Evolis"/> — Evolis needs no server-side machine configuration
    /// (module requirement §1).
    /// </summary>
    /// <param name="TenantId">
    /// Used only by a system-admin caller to target a tenant; for a tenant caller it is ignored
    /// (the token's tenant is used), mirroring <c>CreateProductRequest.TenantId</c>. In practice
    /// every write to this resource is system-admin-only (decision Q-09), so this is normally
    /// how the target tenant is supplied.
    /// </param>
    public sealed record CreatePrinterRequest(
        long BranchId,
        UsingPrinterType UsingPrinterType,
        string Name,
        string Model,
        string UniqueNumber,
        MaticaPrinterConfigRequest? MaticaConfig,
        long? TenantId = null);

    /// <summary>
    /// Update payload for <c>PUT /api/printers/{id}</c> (decision Q-09: system-admin only).
    /// <see cref="UsingPrinterType"/> is deliberately not present — a physical printer's family
    /// is a hardware fact and does not change after registration, unlike a product's printer
    /// family (Q-08), which is a business choice about which machine prints it.
    /// </summary>
    public sealed record UpdatePrinterRequest(
        long BranchId,
        string Name,
        string Model,
        string UniqueNumber,
        MaticaPrinterConfigRequest? MaticaConfig);

    /// <summary>Printer projection returned to clients, including its Matica extension when applicable.</summary>
    public sealed record PrinterResponse(
        long Id,
        long TenantId,
        long BranchId,
        string BranchName,
        UsingPrinterType UsingPrinterType,
        string Name,
        string Model,
        string UniqueNumber,
        MaticaPrinterConfigResponse? MaticaConfig,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt,
        DateTime? DeletedAt);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/printers</c> (decision Q-09: tenant users get
    /// read-only access filtered by type and branch).
    /// </summary>
    /// <param name="UsingPrinterType">Optional printer-family filter; null matches both.</param>
    /// <param name="BranchId">Optional branch filter.</param>
    /// <param name="IsDeleted">Optional soft-delete filter; null matches both.</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="Page">1-based page index. Defaults to 1.</param>
    /// <param name="PageSize">Items per page (max 100 per spec). Defaults to 20.</param>
    /// <param name="SortBy">Optional sort field; mapped to a whitelisted column in the repository.</param>
    /// <param name="SortDir">Sort direction, "asc" or "desc". Defaults to "asc".</param>
    public sealed record PrinterListFilter(
        UsingPrinterType? UsingPrinterType = null,
        long? BranchId = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");

    // =====================================================================================
    //  Ribbon types (Printing Module Q-05)
    // =====================================================================================

    /// <summary>A ribbon type an Evolis configuration can reference.</summary>
    public sealed record RibbonTypeResponse(long Id, string Name);

    // =====================================================================================
    //  Product print configuration (ERD §7, Printing Module Q-02/Q-03/Q-04/Q-05/Q-07/Q-08/Q-09)
    // =====================================================================================

    /// <summary>Matica printing parameters payload (decision Q-03: no separate Font/FontFamily field).</summary>
    public sealed record MaticaPrintConfigRequest(
        int Cpi,
        int FontSize,
        int OffsetX,
        int OffsetY,
        string? ImagePath);

    /// <summary>Matica printing parameters as returned to clients.</summary>
    public sealed record MaticaPrintConfigResponse(
        int Cpi,
        int FontSize,
        int OffsetX,
        int OffsetY,
        string? ImagePath);

    /// <summary>Evolis printing parameters payload. <see cref="RibbonTypeId"/> per decision Q-05.</summary>
    public sealed record EvolisPrintConfigRequest(
        long RibbonTypeId,
        PrintWay PrintWay,
        int X,
        int Y,
        PrintedFace PrintedFace,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle,
        string? ImagePath);

    /// <summary>Evolis printing parameters as returned to clients, with the ribbon type resolved by name.</summary>
    public sealed record EvolisPrintConfigResponse(
        long RibbonTypeId,
        string RibbonTypeName,
        PrintWay PrintWay,
        int X,
        int Y,
        PrintedFace PrintedFace,
        string FontFamily,
        int FontSize,
        string PrintColor,
        string BackgroundColor,
        string FontStyle,
        string? ImagePath);

    /// <summary>
    /// Payload for <c>PUT /api/products/{id}/print-config</c> (decision Q-07: sub-resource, no
    /// standalone POST/DELETE). Also the payload shape for a printer-family switch (decision
    /// Q-08): supplying a different <see cref="UsingPrinterType"/> than the product currently has,
    /// together with that family's payload, switches it — the old configuration row is hard
    /// deleted, not soft deleted, in the same transaction as the new row's insert.
    /// </summary>
    public sealed record UpdateProductPrintConfigRequest(
        UsingPrinterType UsingPrinterType,
        MaticaPrintConfigRequest? Matica,
        EvolisPrintConfigRequest? Evolis);

    /// <summary>
    /// Response for <c>GET</c>/<c>PUT /api/products/{id}/print-config</c>. Exactly one of
    /// <see cref="Matica"/> / <see cref="Evolis"/> is populated, matching
    /// <see cref="UsingPrinterType"/> — the client does not need to guess which one to read.
    /// </summary>
    public sealed record ProductPrintConfigResponse(
        long ProductId,
        UsingPrinterType UsingPrinterType,
        MaticaPrintConfigResponse? Matica,
        EvolisPrintConfigResponse? Evolis);

    /// <summary>
    /// Combined product + print-configuration view for
    /// <c>GET /api/products/{id}/print-config/full</c> (Printing Module, phase 7). System-admin
    /// only. <see cref="PrintConfig"/> is <c>null</c> when the product has no configuration yet —
    /// this endpoint surfaces that gap rather than failing, since it exists specifically as an
    /// administrative overview.
    /// </summary>
    public sealed record ProductWithPrintConfigResponse(
        ProductResponse Product,
        ProductPrintConfigResponse? PrintConfig);

    // =====================================================================================
    //  Print images (module requirements §5/§6/§7, Printing Module Q-10)
    // =====================================================================================

    /// <summary>
    /// Result of <c>POST /api/print-images</c> (decision Q-10). <paramref name="Warning"/> is set
    /// only when the upload replaced an existing image of the same name for this tenant — the
    /// client must never treat <paramref name="Warning"/> being present as a failure; the upload
    /// still succeeded and <paramref name="ImagePath"/> is always the newly saved file.
    /// </summary>
    public sealed record PrintImageUploadResult(string ImagePath, string? Warning);
}
