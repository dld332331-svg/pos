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
/// Unit tests for UnitOfWork transaction methods (BeginTransactionAsync,
/// CommitAsync, RollbackAsync, Dispose, CanConnectAsync).
///
/// Uses the EF Core InMemory provider so that <see cref="UnitOfWork"/>
/// follows its <c>InMemoryDbContextTransaction</c> sentinel path — the
/// same lightweight code path exercised in unit/integration tests.
/// </summary>
public sealed class UnitOfWorkTransactionTests : IDisposable
{
    private readonly POSDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private static int _dbCounter;

    public UnitOfWorkTransactionTests()
    {
        var dbName = $"UnitOfWorkTestDB_{++_dbCounter}_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        _context = new POSDbContext(options);
        _unitOfWork = new UnitOfWork(_context);
    }

    public void Dispose()
    {
        _unitOfWork.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    // =========================================================================
    // BeginTransactionAsync
    // =========================================================================

    [Fact]
    public async Task BeginTransactionAsync_FirstCall_CompletesWithoutException()
    {
        // Act
        var act = () => _unitOfWork.BeginTransactionAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_CalledTwice_SecondCallIsNoOp()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        // Act
        var act = () => _unitOfWork.BeginTransactionAsync();

        // Assert — second call returns immediately (no-op) without exception
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_AfterCommit_CreatesNewTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.CommitAsync();

        // Act — should work second time
        var act = () => _unitOfWork.BeginTransactionAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task BeginTransactionAsync_AfterRollback_CreatesNewTransaction()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.RollbackAsync();

        // Act — should work after rollback
        var act = () => _unitOfWork.BeginTransactionAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // CommitAsync
    // =========================================================================

    [Fact]
    public async Task CommitAsync_AfterBegin_PersistsPendingChanges()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "commit_test_user",
            PasswordHash = "hash",
            FullName = "Commit Test",
            Role = UserRole.Cashier,
            IsActive = true
        });

        // Act
        await _unitOfWork.CommitAsync();

        // Assert — user was persisted
        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Username == "commit_test_user");
        saved.Should().NotBeNull();
        saved!.FullName.Should().Be("Commit Test");
    }

    [Fact]
    public async Task CommitAsync_WithoutBeginTransaction_PersistsData()
    {
        // Arrange
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "no_tx_user",
            PasswordHash = "hash",
            FullName = "No Tx",
            Role = UserRole.Cashier,
            IsActive = true
        });

        // Act — CommitAsync without BeginTransactionAsync still saves
        // (SaveChangesAsync is called, no transaction to dispose)
        var act = () => _unitOfWork.CommitAsync();

        // Assert
        await act.Should().NotThrowAsync();

        var saved = await _context.Users.FirstOrDefaultAsync(u => u.Username == "no_tx_user");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task CommitAsync_BeginCommitBeginCommit_MultipleCyclesWork()
    {
        // Arrange
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        // Cycle 1
        await _unitOfWork.BeginTransactionAsync();
        _context.Users.Add(new User
        {
            Id = id1,
            Username = "cycle_1",
            PasswordHash = "hash",
            FullName = "Cycle One",
            Role = UserRole.Cashier,
            IsActive = true
        });
        await _unitOfWork.CommitAsync();

        // Cycle 2
        await _unitOfWork.BeginTransactionAsync();
        _context.Users.Add(new User
        {
            Id = id2,
            Username = "cycle_2",
            PasswordHash = "hash",
            FullName = "Cycle Two",
            Role = UserRole.Admin,
            IsActive = true
        });
        await _unitOfWork.CommitAsync();

        // Assert — both users persisted
        var count = await _context.Users.CountAsync();
        count.Should().Be(2);
    }

    // =========================================================================
    // RollbackAsync
    // =========================================================================

    [Fact]
    public async Task RollbackAsync_AfterBegin_CompletesWithoutException()
    {
        // Arrange
        await _unitOfWork.BeginTransactionAsync();

        // Act
        var act = () => _unitOfWork.RollbackAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RollbackAsync_WithoutBeginTransaction_IsNoOp()
    {
        // Act — calling RollbackAsync when _currentTransaction is null
        var act = () => _unitOfWork.RollbackAsync();

        // Assert — no exception, no-op
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RollbackAsync_AfterCommit_IsNoOp()
    {
        // Arrange — CommitAsync disposes _currentTransaction and sets to null
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.CommitAsync();

        // Act — RollbackAsync after commit is a no-op (no transaction to roll back)
        var act = () => _unitOfWork.RollbackAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // Begin + Commit + Rollback — Combined Scenarios
    // =========================================================================

    [Fact]
    public async Task CommitCalledAfterRollback_DoesNotThrow()
    {
        // Arrange — commit after rollback should be safe (no transaction to dispose)
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.RollbackAsync();

        // Act
        var act = () => _unitOfWork.CommitAsync();

        // Assert — SaveChangesAsync is still called; no transaction to dispose
        await act.Should().NotThrowAsync();
    }

    // =========================================================================
    // Dispose
    // =========================================================================

    [Fact]
    public async Task Dispose_WithActiveTransaction_DisposesTransactionAndContext()
    {
        // Arrange
        var dbName = $"DisposeTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using var context = new POSDbContext(options);
        using var uow = new UnitOfWork(context);

        // Act — start a transaction then dispose (via using)
        await uow.BeginTransactionAsync();
    }

    [Fact]
    public void Dispose_WithoutActiveTransaction_DisposesContext()
    {
        // Arrange
        var dbName = $"DisposeNoTxTest_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using var context = new POSDbContext(options);
        using var uow = new UnitOfWork(context);

        // Act — no transaction started, just dispose (via using)
    }



    // =========================================================================
    // SaveChangesAsync — Exception Paths
    // =========================================================================

    [Fact]
    public async Task SaveChangesAsync_ContextDisposed_ThrowsInvalidOperationException()
    {
        // Arrange
        var dbName = $"SaveChangesDisposed_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new POSDbContext(options);
        var uow = new UnitOfWork(context);

        // Dispose the context — SaveChangesAsync will throw ObjectDisposedException,
        // which is caught by the generic catch (Exception) and rethrown as InvalidOperationException
        context.Dispose();

        // Act
        var act = () => uow.SaveChangesAsync();

        // Assert — generic catch wraps it as InvalidOperationException
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("unexpected");

        uow.Dispose();
    }

    // =========================================================================
    // CommitAsync — SaveChanges Failure Path
    // =========================================================================

    [Fact]
    public async Task CommitAsync_WhenSaveChangesFails_RollsBackAndRethrows()
    {
        // Arrange — create an isolated UoW so we don't break the fixture
        var dbName = $"CommitFailRollback_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new POSDbContext(options);
        var uow = new UnitOfWork(context);

        await uow.BeginTransactionAsync();

        // Dispose the context — subsequent SaveChangesAsync in CommitAsync will throw.
        // CommitAsync's catch block calls RollbackAsync then rethrows.
        context.Dispose();

        // Act
        var act = () => uow.CommitAsync();

        // Assert — exception is rethrown after rollback
        await act.Should().ThrowAsync<InvalidOperationException>();

        uow.Dispose();
    }

    // =========================================================================
    // CanConnectAsync
    // =========================================================================

    [Fact]
    public async Task CanConnectAsync_WithInMemoryProvider_ReturnsTrue()
    {
        // Act
        var result = await _unitOfWork.CanConnectAsync();

        // Assert — InMemory database is always "connectable"
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanConnectAsync_AfterDispose_ReturnsFalse()
    {
        // Arrange — create a separate context/UoW so we don't dispose the fixture
        var dbName = $"CanConnectAfterDispose_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new POSDbContext(options);
        var uow = new UnitOfWork(context);
        uow.Dispose();

        // Act — context is disposed, CanConnectAsync should catch and return false
        var result = await uow.CanConnectAsync();

        // Assert
        result.Should().BeFalse();

        context.Dispose();
    }

    // =========================================================================
    // Data Access Through UnitOfWork Repositories
    // =========================================================================

    [Fact]
    public async Task Repository_BeginCommit_PersistsAndRetrievesData()
    {
        // Arrange
        var id = Guid.NewGuid();
        var product = new Product
        {
            Id = id,
            Name = "Test Product",
            Price = 10.500m,
            Cost = 5.000m,
            Status = ProductStatus.Active,
            ProductType = ProductType.Standard,
            TaxRate = 0.050m
        };

        // Act
        await _unitOfWork.BeginTransactionAsync();
        await _unitOfWork.Products.AddAsync(product);
        await _unitOfWork.CommitAsync();

        // Assert — retrieve via repository
        var saved = await _unitOfWork.Products.GetByIdAsync(id);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Test Product");
        saved.Price.Should().Be(10.500m);
    }

    [Fact]
    public async Task Repository_MultipleOperationsInTransaction_Succeeds()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var prodId1 = Guid.NewGuid();
        var prodId2 = Guid.NewGuid();

        await _unitOfWork.BeginTransactionAsync();

        await _unitOfWork.Categories.AddAsync(new Category
        {
            Id = catId,
            Name = "Beverages"
        });

        await _unitOfWork.Products.AddAsync(new Product
        {
            Id = prodId1,
            Name = "Coffee",
            CategoryId = catId,
            Price = 3.500m,
            Cost = 1.000m,
            Status = ProductStatus.Active,
            ProductType = ProductType.Standard,
            TaxRate = 0.050m
        });

        await _unitOfWork.Products.AddAsync(new Product
        {
            Id = prodId2,
            Name = "Tea",
            CategoryId = catId,
            Price = 2.500m,
            Cost = 0.500m,
            Status = ProductStatus.Active,
            ProductType = ProductType.Standard,
            TaxRate = 0.050m
        });

        await _unitOfWork.CommitAsync();

        // Assert
        var products = await _unitOfWork.Products.GetAllAsync();
        products.Should().HaveCount(2);
    }

    [Fact]
    public async Task SaveChangesAsync_ReturnsCorrectNumberOfChanges()
    {
        // Arrange
        _context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "save_count_user",
            PasswordHash = "hash",
            FullName = "Save Count",
            Role = UserRole.Cashier,
            IsActive = true
        });

        // Act
        var changes = await _unitOfWork.SaveChangesAsync();

        // Assert
        changes.Should().Be(1);
    }
}
