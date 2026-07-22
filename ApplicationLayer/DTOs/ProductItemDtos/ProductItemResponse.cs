using System;
using DomainLayer.Enums;

namespace ApplicationLayer.DTOs.ProductItems
{
    /// <summary>
    /// Product-item projection (API Spec §4.7). The PAN is never returned in full:
    /// <see cref="MaskedPan"/> shows six masking characters followed by the last six characters
    /// of <c>EncryptedPan</c> (e.g. <c>******123456</c>).
    /// </summary>
    public sealed record ProductItemResponse(
        long Id,
        long TenantId,
        string MaskedPan,
        long ProductId,
        string ProductName,
        long BranchId,
        long BatchId,
        CardStatus Status,
        string? HolderName,
        string? Notes,
        bool IsDeleted,
        DateTime CreatedAt,
        DateTime? UpdatedAt);

    /// <summary>
    /// Update payload for <c>PUT /api/product-items/{id}</c> (API Spec §4.7). Changing
    /// <see cref="Status"/> transactionally recomputes the branch stock aggregate.
    /// </summary>
    public sealed record UpdateProductItemRequest(
        CardStatus Status,
        string? HolderName,
        string? Notes);

    /// <summary>
    /// Filter/paging inputs for <c>GET /api/product-items</c> (API Spec §4.7).
    /// </summary>
    /// <param name="Code">Prefix match against the stored <c>EncryptedPan</c>.</param>
    /// <param name="ProductId">Optional exact product filter.</param>
    /// <param name="ProductName">Optional case-insensitive product-name contains-filter.</param>
    /// <param name="Status">Optional status filter.</param>
    /// <param name="BranchId">Optional branch filter.</param>
    /// <param name="IsDeleted">Tri-state (null = both).</param>
    /// <param name="TenantId">System-admin-only tenant filter; ignored for tenant callers.</param>
    /// <param name="SortBy">code | status | productid | branchid | createdat (default).</param>
    /// <param name="SortDir">asc (default) or desc.</param>
    public sealed record ProductItemListFilter(
        string? Code = null,
        long? ProductId = null,
        string? ProductName = null,
        CardStatus? Status = null,
        long? BranchId = null,
        bool? IsDeleted = null,
        long? TenantId = null,
        int Page = 1,
        int PageSize = 20,
        string? SortBy = null,
        string? SortDir = "asc");
}