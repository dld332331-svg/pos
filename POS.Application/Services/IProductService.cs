using POS.Application.DTOs;

namespace POS.Application.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetProductsAsync(ProductFilterDto filter);
    Task<ProductDto?> GetProductByIdAsync(Guid id);
    Task<ProductDto> CreateProductAsync(CreateProductRequest request);
    Task<ProductDto> UpdateProductAsync(UpdateProductRequest request);
    Task<OperationResult> ArchiveProductAsync(Guid id, string reason);
    Task<ProductDto?> FindByBarcodeAsync(string barcode);
    Task<ProductDto?> FindBySkuAsync(string sku);
    Task<List<ProductDto>> GetLowStockProductsAsync();
    Task<List<CategoryDto>> GetCategoriesAsync();
    Task<CategoryDto> CreateCategoryAsync(string name, Guid? parentId);

    /// <summary>
    /// Gets all active modifier groups with their modifiers and sizes.
    /// Used by the POS form to show modifier selection dialogs.
    /// </summary>
    Task<List<ModifierGroupDto>> GetModifierGroupsAsync();

    /// <summary>
    /// Gets all active units of measure.
    /// Used by ProductForm and PosTerminalForm for unit selection.
    /// </summary>
    Task<List<UnitOfMeasureDto>> GetUnitsOfMeasureAsync();
}