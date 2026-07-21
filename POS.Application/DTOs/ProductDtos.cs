namespace POS.Application.DTOs;

public record ProductDto(Guid Id, string Name, string? ArabicName, string? Sku, string? Barcode, Guid? CategoryId, string? CategoryName, string ProductType, string? Unit, decimal Cost, decimal Price, decimal TaxRate, decimal MinStock, string? SupplierName, string? ImagePath, string Status, bool AllowModifiers, decimal CurrentStock)
{
    // Backward compatibility aliases for forms referencing old property names
    public decimal SellingPrice => Price;
    public string? EnglishName => Name;
}

public record UnitOfMeasureDto(Guid Id, string Name, string? ArabicName, string Symbol, string? ArabicSymbol, string Category, decimal ConversionFactor, bool IsBaseUnit, int DecimalPlaces);
public record CreateProductRequest(string Name, string? ArabicName, string? Sku, string? Barcode, Guid? CategoryId, string ProductType, string? Unit, decimal Cost, decimal Price, decimal TaxRate, decimal MinStock, Guid? SupplierId, bool AllowModifiers);
public record UpdateProductRequest(Guid Id, string Name, string? ArabicName, string? Sku, string? Barcode, Guid? CategoryId, string ProductType, string? Unit, decimal Cost, decimal Price, decimal TaxRate, decimal MinStock, Guid? SupplierId, bool AllowModifiers, string Status);
public record ProductFilterDto(string? SearchTerm, Guid? CategoryId, string? ProductType, string? Status, int Page = 1, int PageSize = 20);
public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize);
