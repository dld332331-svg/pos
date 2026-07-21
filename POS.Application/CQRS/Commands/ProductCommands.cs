using POS.Application.CQRS.Abstractions;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Application.CQRS.Commands;

// ─── Command Records ───────────────────────────────────────────────────────

public record CreateProductCommand(
    string Name, string? ArabicName, string? Sku, string? Barcode,
    Guid? CategoryId, string ProductType, string? Unit,
    decimal Cost, decimal Price, decimal TaxRate, decimal MinStock,
    Guid? SupplierId, bool AllowModifiers) : ICommand<ProductDto>;

public record UpdateProductCommand(
    Guid Id, string Name, string? ArabicName, string? Sku, string? Barcode,
    Guid? CategoryId, string ProductType, string? Unit,
    decimal Cost, decimal Price, decimal TaxRate, decimal MinStock,
    Guid? SupplierId, bool AllowModifiers, string Status) : ICommand<ProductDto>;

public record ArchiveProductCommand(Guid Id, string Reason) : ICommand<OperationResult>;
public record CreateCategoryCommand(string Name, Guid? ParentId) : ICommand<CategoryDto>;

// ─── Handlers (delegate to existing services) ──────────────────────────────

public sealed class CreateProductCommandHandler(IProductService service) : ICommandHandler<CreateProductCommand, ProductDto>
{
    public Task<ProductDto> HandleAsync(CreateProductCommand cmd, CancellationToken ct = default)
    {
        var request = new CreateProductRequest(cmd.Name, cmd.ArabicName, cmd.Sku, cmd.Barcode,
            cmd.CategoryId, cmd.ProductType, cmd.Unit, cmd.Cost, cmd.Price, cmd.TaxRate,
            cmd.MinStock, cmd.SupplierId, cmd.AllowModifiers);
        return service.CreateProductAsync(request);
    }
}

public sealed class UpdateProductCommandHandler(IProductService service) : ICommandHandler<UpdateProductCommand, ProductDto>
{
    public Task<ProductDto> HandleAsync(UpdateProductCommand cmd, CancellationToken ct = default)
    {
        var request = new UpdateProductRequest(cmd.Id, cmd.Name, cmd.ArabicName, cmd.Sku, cmd.Barcode,
            cmd.CategoryId, cmd.ProductType, cmd.Unit, cmd.Cost, cmd.Price, cmd.TaxRate,
            cmd.MinStock, cmd.SupplierId, cmd.AllowModifiers, cmd.Status);
        return service.UpdateProductAsync(request);
    }
}

public sealed class ArchiveProductCommandHandler(IProductService service) : ICommandHandler<ArchiveProductCommand, OperationResult>
{
    public Task<OperationResult> HandleAsync(ArchiveProductCommand cmd, CancellationToken ct = default)
        => service.ArchiveProductAsync(cmd.Id, cmd.Reason);
}

public sealed class CreateCategoryCommandHandler(IProductService service) : ICommandHandler<CreateCategoryCommand, CategoryDto>
{
    public Task<CategoryDto> HandleAsync(CreateCategoryCommand cmd, CancellationToken ct = default)
        => service.CreateCategoryAsync(cmd.Name, cmd.ParentId);
}
