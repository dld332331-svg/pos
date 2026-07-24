#nullable enable

using Xunit;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Database;
using POS.Infrastructure.Repositories;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="Repository{T}.GetPagedAsync"/> covering
/// clamping logic (pageNumber, pageSize) and pagination behavior.
/// Uses EF Core InMemory provider.
/// </summary>
public sealed class RepositoryGetPagedAsyncTests : IDisposable
{
    private readonly POSDbContext _context;
    private readonly Repository<Product> _repository;
    private static int _dbCounter;

    public RepositoryGetPagedAsyncTests()
    {
        var dbName = $"RepoPagedTest_{++_dbCounter}_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        _context = new POSDbContext(options);
        _repository = new Repository<Product>(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private static Product CreateProduct(string name = "Test", decimal price = 10m)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Name = name,
            Price = price,
            Cost = 5m,
            Status = ProductStatus.Active,
            ProductType = ProductType.Standard,
            TaxRate = 0.050m
        };
    }

    /// <summary>
    /// Seeds the repository with a given number of products.
    /// </summary>
    private async Task SeedProductsAsync(int count)
    {
        for (var i = 1; i <= count; i++)
        {
            var product = CreateProduct($"Product_{i}", price: i * 10m);
            // Simulate CreatedAt ordering by spacing timestamps
            product.CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            await _repository.AddAsync(product);
        }
        await _context.SaveChangesAsync();
    }

    // ========================================================================
    // PageNumber Clamping
    // ========================================================================

    [Fact]
    public async Task GetPagedAsync_PageNumberZero_ClampsToOne()
    {
        await SeedProductsAsync(5);

        var (items, total) = await _repository.GetPagedAsync(0, 10);

        items.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_PageNumberNegative_ClampsToOne()
    {
        await SeedProductsAsync(5);

        var (items, total) = await _repository.GetPagedAsync(-5, 10);

        items.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_PageNumberOne_FirstPage()
    {
        await SeedProductsAsync(20);

        var (items, total) = await _repository.GetPagedAsync(1, 10);

        items.Should().HaveCount(10);
        total.Should().Be(20);
    }

    // ========================================================================
    // PageSize Clamping
    // ========================================================================

    [Fact]
    public async Task GetPagedAsync_PageSizeZero_ClampsToTen()
    {
        await SeedProductsAsync(5);

        var (items, total) = await _repository.GetPagedAsync(1, 0);

        items.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_PageSizeNegative_ClampsToTen()
    {
        await SeedProductsAsync(5);

        var (items, total) = await _repository.GetPagedAsync(1, -3);

        items.Should().HaveCount(5);
        total.Should().Be(5);
    }

    [Fact]
    public async Task GetPagedAsync_PageSizeTen_ReturnsUpToTen()
    {
        await SeedProductsAsync(25);

        var (items, total) = await _repository.GetPagedAsync(1, 10);

        items.Should().HaveCount(10);
        total.Should().Be(25);
    }

    // ========================================================================
    // Pagination — Multi-Page Scenarios
    // ========================================================================

    [Fact]
    public async Task GetPagedAsync_PageTwo_ReturnsSecondPage()
    {
        await SeedProductsAsync(25);

        var (items, total) = await _repository.GetPagedAsync(2, 10);

        items.Should().HaveCount(10);
        total.Should().Be(25);
    }

    [Fact]
    public async Task GetPagedAsync_LastPagePartial_ReturnsRemaining()
    {
        await SeedProductsAsync(25);

        var (items, total) = await _repository.GetPagedAsync(3, 10);

        items.Should().HaveCount(5);
        total.Should().Be(25);
    }

    [Fact]
    public async Task GetPagedAsync_PageBeyondTotal_ReturnsEmpty()
    {
        await SeedProductsAsync(5);

        var (items, total) = await _repository.GetPagedAsync(10, 10);

        items.Should().BeEmpty();
        total.Should().Be(5);
    }

    // ========================================================================
    // Edge Cases
    // ========================================================================

    [Fact]
    public async Task GetPagedAsync_EmptyRepository_ReturnsEmpty()
    {
        var (items, total) = await _repository.GetPagedAsync(1, 10);

        items.Should().BeEmpty();
        total.Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_ExcludesSoftDeleted()
    {
        // Seed one active product
        var active = CreateProduct("Active");
        await _repository.AddAsync(active);

        // Add and soft-delete a second product directly via context
        var deleted = CreateProduct("Deleted");
        await _repository.AddAsync(deleted);
        deleted.MarkAsDeleted();
        await _context.SaveChangesAsync();

        var (items, total) = await _repository.GetPagedAsync(1, 10);

        items.Should().HaveCount(1);
        items.Should().Contain(p => p.Name == "Active");
        total.Should().Be(1);
    }

    [Fact]
    public async Task GetPagedAsync_ReturnsOrderedByCreatedAtDescending()
    {
        // Create 3 products with staggered CreatedAt
        var p1 = CreateProduct("First");
        p1.CreatedAt = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        await _repository.AddAsync(p1);

        var p2 = CreateProduct("Second");
        p2.CreatedAt = new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);
        await _repository.AddAsync(p2);

        var p3 = CreateProduct("Third");
        p3.CreatedAt = new DateTime(2026, 7, 3, 10, 0, 0, DateTimeKind.Utc);
        await _repository.AddAsync(p3);

        await _context.SaveChangesAsync();

        var (items, _) = await _repository.GetPagedAsync(1, 10);

        items.Should().HaveCount(3);
        items[0].Name.Should().Be("Third");
        items[1].Name.Should().Be("Second");
        items[2].Name.Should().Be("First");
    }
}
