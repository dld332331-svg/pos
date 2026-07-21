using POS.Application.DTOs;
using POS.Application.Validators;
using POS.Domain.BusinessRules;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Application.Services.Implementations;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;

    public ProductService(IUnitOfWork unitOfWork, IAuditService auditService)
    {
        _unitOfWork = unitOfWork;
        _auditService = auditService;
    }

    public async Task<PagedResult<ProductDto>> GetProductsAsync(ProductFilterDto filter)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var allProducts = await _unitOfWork.Products.GetAllAsync();
        var allCategories = await _unitOfWork.Categories.GetAllAsync();
        var allInventory = await _unitOfWork.InventoryItems.GetAllAsync();

        var categoryMap = allCategories.ToDictionary(c => c.Id, c => c.Name);
        var inventoryMap = allInventory.ToDictionary(i => i.ProductId, i => i.Quantity);

        var filtered = allProducts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim().ToLower();
            filtered = filtered.Where(p =>
                (p.ArabicName ?? string.Empty).ToLower().Contains(term) ||
                (p.Name != null && p.Name.ToLower().Contains(term)) ||
                (p.Sku != null && p.Sku.ToLower().Contains(term)) ||
                (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (filter.CategoryId.HasValue)
            filtered = filtered.Where(p => p.CategoryId == filter.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(filter.ProductType) && Enum.TryParse<ProductType>(filter.ProductType, out var pType))
            filtered = filtered.Where(p => p.ProductType == pType);

        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<ProductStatus>(filter.Status, out var pStatus))
            filtered = filtered.Where(p => p.Status == pStatus);

        var total = filtered.Count();

        var items = filtered
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(p => MapToDto(p, categoryMap, inventoryMap))
            .ToList();

        return new PagedResult<ProductDto>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<ProductDto?> GetProductByIdAsync(Guid id)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null) return null;

        return await MapToDtoAsync(product);
    }

    public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = ProductValidator.ValidateCreate(request);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(", ", errors));

        var product = new Product
        {
            Name = request.Name ?? string.Empty,
            ArabicName = request.ArabicName,
            Sku = request.Sku ?? string.Empty,
            Barcode = request.Barcode,
            CategoryId = request.CategoryId ?? Guid.Empty,
            ProductType = Enum.TryParse<ProductType>(request.ProductType, out var pt) ? pt : ProductType.Standard,
            Unit = request.Unit ?? "piece",
            Cost = MoneyPolicy.RoundToJOD(request.Cost),
            Price = MoneyPolicy.RoundToJOD(request.Price),
            TaxRate = request.TaxRate / 100m,
            MinStock = request.MinStock,
            SupplierId = request.SupplierId,
            AllowModifiers = request.AllowModifiers,
            Status = ProductStatus.Active
        };

        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // Create initial inventory record
        var inventoryItem = new InventoryItem
        {
            ProductId = product.Id,
            Quantity = 0,
            ReservedQuantity = 0
        };
        await _unitOfWork.InventoryItems.AddAsync(inventoryItem);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.ProductCreated, "Product", product.Id, null, null, null);

        return (await MapToDtoAsync(product))!;
    }

    public async Task<ProductDto> UpdateProductAsync(UpdateProductRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var existing = await _unitOfWork.Products.GetByIdAsync(request.Id);
        if (existing is null)
            throw new InvalidOperationException("المنتج غير موجود");

        var beforeValue = $"ArabicName={existing.ArabicName},SellingPrice={existing.Price}";

        existing.ArabicName = request.ArabicName;
        existing.Name = request.Name ?? string.Empty;
        existing.Sku = request.Sku ?? string.Empty;
        existing.Barcode = request.Barcode;
        existing.CategoryId = request.CategoryId ?? Guid.Empty;
        existing.ProductType = Enum.TryParse<ProductType>(request.ProductType, out var pt) ? pt : ProductType.Standard;
        existing.Unit = request.Unit ?? "piece";
        existing.Cost = MoneyPolicy.RoundToJOD(request.Cost);
        existing.Price = MoneyPolicy.RoundToJOD(request.Price);
        existing.TaxRate = request.TaxRate / 100m;
        existing.MinStock = request.MinStock;
        existing.SupplierId = request.SupplierId;
        existing.AllowModifiers = request.AllowModifiers;
        existing.Status = Enum.TryParse<ProductStatus>(request.Status, out var ps) ? ps : existing.Status;
        existing.MarkAsModified();

        await _unitOfWork.Products.UpdateAsync(existing);
        await _unitOfWork.SaveChangesAsync();

        var afterValue = $"ArabicName={existing.ArabicName},SellingPrice={existing.Price}";
        await _auditService.LogAsync(null, AuditActionType.ProductUpdated, "Product", existing.Id, beforeValue, afterValue, null);

        return (await MapToDtoAsync(existing))!;
    }

    public async Task<OperationResult> ArchiveProductAsync(Guid id, string reason)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(id);
        if (product is null)
            return new OperationResult(false, ErrorMessage: "المنتج غير موجود");

        product.Status = ProductStatus.Archived;
        product.MarkAsModified();

        await _unitOfWork.Products.UpdateAsync(product);
        await _unitOfWork.SaveChangesAsync();

        await _auditService.LogAsync(null, AuditActionType.ProductArchived, "Product", product.Id, null, null, reason);

        return new OperationResult(true, SuccessMessage: "تم أرشفة المنتج بنجاح");
    }

    public async Task<ProductDto?> FindByBarcodeAsync(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return null;

        var results = await _unitOfWork.Products.FindAsync(p => p.Barcode == barcode && p.Status == ProductStatus.Active);
        var product = results.FirstOrDefault();
        if (product is null) return null;

        return await MapToDtoAsync(product);
    }

    public async Task<ProductDto?> FindBySkuAsync(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku)) return null;

        var results = await _unitOfWork.Products.FindAsync(p => p.Sku == sku && p.Status == ProductStatus.Active);
        var product = results.FirstOrDefault();
        if (product is null) return null;

        return await MapToDtoAsync(product);
    }

    public async Task<List<ProductDto>> GetLowStockProductsAsync()
    {
        var products = await _unitOfWork.Products.GetAllAsync();
        var inventory = await _unitOfWork.InventoryItems.GetAllAsync();
        var inventoryMap = inventory.ToDictionary(i => i.ProductId, i => i.Quantity);

        var lowStock = products
            .Where(p => p.Status == ProductStatus.Active)
            .Where(p => inventoryMap.TryGetValue(p.Id, out var qty) && qty <= p.MinStock)
            .ToList();

        var result = new List<ProductDto>();
        foreach (var p in lowStock)
        {
            var dto = await MapToDtoAsync(p);
            if (dto is not null) result.Add(dto);
        }
        return result;
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync()
    {
        var categories = await _unitOfWork.Categories.GetAllAsync();
        var products = await _unitOfWork.Products.GetAllAsync();
        var productCounts = products
            .Where(p => !p.IsDeleted && p.Status == ProductStatus.Active)
            .GroupBy(p => p.CategoryId)
            .ToDictionary(g => g.Key, g => g.Count());

        return categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.SortOrder)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.ParentCategoryId,
                c.SortOrder,
                c.IsActive,
                productCounts.GetValueOrDefault(c.Id, 0)))
            .ToList();
    }

    public async Task<List<ModifierGroupDto>> GetModifierGroupsAsync()
    {
        // Load groups, modifiers, and sizes in separate queries to
        // avoid requiring eager-loading support from the repository
        var groups = await _unitOfWork.ModifierGroups.FindAsync(g => g.IsActive);
        var allModifiers = await _unitOfWork.Modifiers.FindAsync(m => m.IsActive);
        var allSizes = await _unitOfWork.ModifierSizes.GetAllAsync();

        var modifiersByGroup = allModifiers
            .GroupBy(m => m.ModifierGroupId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var sizesByModifier = allSizes
            .GroupBy(s => s.ModifierId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ModifierGroupDto>();
        foreach (var group in groups.OrderBy(g => g.SortOrder))
        {
            var groupModifiers = modifiersByGroup.GetValueOrDefault(group.Id, new List<Modifier>());

            var modifierDtos = groupModifiers.Select(m =>
            {
                var mSizes = sizesByModifier.GetValueOrDefault(m.Id, new List<ModifierSize>());
                return new ModifierDto(
                    m.Id,
                    m.Name,
                    m.ArabicName,
                    m.Price,
                    mSizes.Select(s => new ModifierSizeDto(
                        s.Id,
                        s.Name,
                        s.ArabicName,
                        s.Price,
                        s.PriceAdjustment)).ToList());
            }).ToList();

            result.Add(new ModifierGroupDto(
                group.Id,
                group.Name,
                group.ArabicName,
                group.IsRequired,
                group.MinSelections,
                group.MaxSelections,
                group.SortOrder,
                modifierDtos));
        }

        return result;
    }

    public async Task<CategoryDto> CreateCategoryAsync(string name, Guid? parentId)
    {
        var category = new Category
        {
            Name = name,
            ParentCategoryId = parentId,
            SortOrder = 0,
            IsActive = true
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        return new CategoryDto(category.Id, category.Name, category.ParentCategoryId, category.SortOrder, category.IsActive, 0);
    }

    private async Task<ProductDto?> MapToDtoAsync(Product product)
    {
        string? categoryName = null;
        decimal currentStock = 0;
        string? supplierName = null;

        if (product.CategoryId != Guid.Empty)
        {
            var category = await _unitOfWork.Categories.GetByIdAsync(product.CategoryId);
            categoryName = category?.Name;
        }

        var inventory = await _unitOfWork.InventoryItems.FindAsync(i => i.ProductId == product.Id);
        var invItem = inventory.FirstOrDefault();
        if (invItem is not null)
            currentStock = invItem.Quantity;

        if (product.SupplierId.HasValue)
        {
            var supplier = await _unitOfWork.Suppliers.GetByIdAsync(product.SupplierId.Value);
            supplierName = supplier?.Name;
        }

        return new ProductDto(
            product.Id,
            product.ArabicName ?? string.Empty,
            product.Name,
            product.Sku,
            product.Barcode,
            product.CategoryId != Guid.Empty ? product.CategoryId : null,
            categoryName,
            product.ProductType.ToString(),
            product.Unit,
            product.Cost,
            product.Price,
            product.TaxRate,
            product.MinStock,
            supplierName,
            product.ImagePath,
            product.Status.ToString(),
            product.AllowModifiers,
            currentStock);
    }

    private static ProductDto MapToDto(Product product, Dictionary<Guid, string> categoryMap, Dictionary<Guid, decimal> inventoryMap)
    {
        categoryMap.TryGetValue(product.CategoryId, out var categoryName);
        inventoryMap.TryGetValue(product.Id, out var currentStock);

        return new ProductDto(
            product.Id,
            product.ArabicName ?? string.Empty,
            product.Name,
            product.Sku,
            product.Barcode,
            product.CategoryId != Guid.Empty ? product.CategoryId : null,
            categoryName,
            product.ProductType.ToString(),
            product.Unit,
            product.Cost,
            product.Price,
            product.TaxRate,
            product.MinStock,
            null,
            product.ImagePath,
            product.Status.ToString(),
            product.AllowModifiers,
            currentStock);
    }

    public async Task<List<UnitOfMeasureDto>> GetUnitsOfMeasureAsync()
    {
        var units = await _unitOfWork.UnitOfMeasures.FindAsync(u => u.IsActive);
        return units.OrderBy(u => u.Category).ThenBy(u => u.SortOrder)
            .Select(u => new UnitOfMeasureDto(
                u.Id, u.Name, u.ArabicName, u.Symbol, u.ArabicSymbol,
                u.Category, u.ConversionFactor, u.IsBaseUnit, u.DecimalPlaces))
            .ToList();
    }
}