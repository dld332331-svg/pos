using POS.Application.DTOs;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public SupplierService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<List<SupplierDto>> GetSuppliersAsync(string? search = null)
    {
        var allSuppliers = await _unitOfWork.Suppliers.GetAllAsync();

        var suppliers = allSuppliers.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            suppliers = suppliers.Where(s =>
                s.Name.ToLower().Contains(term) ||
                (s.ArabicName ?? "").ToLower().Contains(term) ||
                (s.Phone ?? "").Contains(term) ||
                (s.Email ?? "").ToLower().Contains(term) ||
                (s.ContactPerson ?? "").ToLower().Contains(term));
        }

        return suppliers
            .OrderBy(s => s.Name)
            .Select(s => new SupplierDto(
                s.Id,
                s.Name,
                s.ContactPerson,
                s.Phone,
                s.Email,
                s.Address,
                s.Balance,
                s.IsActive))
            .ToList();
    }

    public async Task<SupplierDto> CreateSupplierAsync(string name, string? contactPerson, string? phone, string? email, string? address)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("اسم المورد مطلوب");

        // Check for duplicate name
        var existing = await _unitOfWork.Suppliers.FindAsync(s =>
            s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing.Any())
            throw new InvalidOperationException("يوجد مورد آخر بنفس الاسم");

        var supplier = new Supplier
        {
            Name = name,
            ArabicName = name,
            ContactPerson = contactPerson,
            Phone = phone,
            Email = email,
            Address = address,
            Balance = 0,
            IsActive = true
        };

        await _unitOfWork.Suppliers.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.SettingChanged, "Supplier", supplier.Id,
            null, $"Name={name},Phone={phone}", null);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Balance,
            supplier.IsActive);
    }

    public async Task<SupplierDto> UpdateSupplierAsync(Guid id, string name, string? contactPerson, string? phone, string? email, string? address)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(id)
            ?? throw new InvalidOperationException("المورد غير موجود");

        var beforeValue = $"Name={supplier.Name},Phone={supplier.Phone},Email={supplier.Email},Active={supplier.IsActive}";

        supplier.Name = name;
        supplier.ArabicName = name;
        supplier.ContactPerson = contactPerson;
        supplier.Phone = phone;
        supplier.Email = email;
        supplier.Address = address;
        supplier.MarkAsModified();

        await _unitOfWork.Suppliers.UpdateAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"Name={name},Phone={phone},Email={email},Active={supplier.IsActive}";
        await _auditService.LogAsync(null, AuditActionType.SettingChanged, "Supplier", id,
            beforeValue, afterValue, null);

        return new SupplierDto(
            supplier.Id,
            supplier.Name,
            supplier.ContactPerson,
            supplier.Phone,
            supplier.Email,
            supplier.Address,
            supplier.Balance,
            supplier.IsActive);
    }

    public async Task<List<PurchaseOrderDto>> GetSupplierOrdersAsync(Guid supplierId)
    {
        var supplier = await _unitOfWork.Suppliers.GetByIdAsync(supplierId)
            ?? throw new InvalidOperationException("المورد غير موجود");

        var allOrders = await _unitOfWork.PurchaseOrders.GetAllAsync();
        var supplierOrders = allOrders
            .Where(po => po.SupplierId == supplierId)
            .OrderByDescending(po => po.CreatedAt)
            .ToList();

        var result = new List<PurchaseOrderDto>();
        foreach (var po in supplierOrders)
        {
            var poItems = await _unitOfWork.PurchaseOrderItems.FindAsync(i => i.PurchaseOrderId == po.Id);
            var itemDtos = poItems.Select(i => new PurchaseOrderItemDto(
                i.InventoryItemId,
                i.ItemName ?? "Unknown",
                i.Quantity,
                i.UnitCost,
                i.TotalCost,
                i.ReceivedQuantity)).ToList();

            result.Add(new PurchaseOrderDto(
                po.Id,
                po.OrderNumber,
                supplier.Name,
                po.TotalAmount,
                po.Status,
                po.CreatedAt,
                itemDtos,
                po.Notes));
        }

        return result;
    }
}
