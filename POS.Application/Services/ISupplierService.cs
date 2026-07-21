using POS.Application.DTOs;

namespace POS.Application.Services;

/// <summary>
/// Service for managing supplier records.
/// </summary>
public interface ISupplierService
{
    /// <summary>
    /// Gets all active suppliers.
    /// </summary>
    Task<List<SupplierDto>> GetSuppliersAsync(string? search = null);

    /// <summary>
    /// Creates a new supplier.
    /// </summary>
    Task<SupplierDto> CreateSupplierAsync(string name, string? contactPerson, string? phone, string? email, string? address);

    /// <summary>
    /// Updates an existing supplier.
    /// </summary>
    Task<SupplierDto> UpdateSupplierAsync(Guid id, string name, string? contactPerson, string? phone, string? email, string? address);

    /// <summary>
    /// Gets purchase order history for a supplier.
    /// </summary>
    Task<List<PurchaseOrderDto>> GetSupplierOrdersAsync(Guid supplierId);
}
