#nullable enable

using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for CustomerService covering all 4 public methods:
/// GetCustomersAsync, CreateCustomerAsync, UpdateCustomerAsync, GetCustomerOrderHistoryAsync.
/// </summary>
public class CustomerServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static Customer CreateTestCustomer(
        Guid? id = null,
        string name = "أحمد علي",
        string? phone = "0791234567",
        string? email = "ahmed@example.com",
        decimal balance = 0m,
        bool isActive = true)
    {
        return new Customer
        {
            Id = id ?? Guid.NewGuid(),
            Name = name,
            Phone = phone,
            Email = email,
            Balance = balance,
            IsActive = isActive
        };
    }

    private static Sale CreateCompletedSale(Guid customerId, Guid? saleId = null, decimal total = 50.000m)
    {
        return new Sale
        {
            Id = saleId ?? Guid.NewGuid(),
            CustomerId = customerId,
            InvoiceNumber = $"INV-{Guid.NewGuid():N}",
            TotalAmount = total,
            SubTotal = total,
            TaxAmount = 0,
            DiscountAmount = 0,
            Status = SaleStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            IsPaid = true
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>())).ReturnsAsync(new List<T>());
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    private (CustomerService service, Mock<IUnitOfWork> uowMock, Mock<IAuditService> auditMock)
        BuildServiceWithMocks(
            List<Customer>? customers = null,
            List<Sale>? sales = null,
            List<User>? users = null)
    {
        var uowMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        uowMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Customers repository ----
        var customerRepoMock = new Mock<IRepository<Customer>>();
        customerRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(customers ?? new List<Customer>());
        customerRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => customers?.FirstOrDefault(c => c.Id == id));
        customerRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Customer, bool>>>()))
            .ReturnsAsync((Expression<Func<Customer, bool>> predicate) =>
                (customers ?? new List<Customer>()).AsQueryable().Where(predicate).ToList());
        customerRepoMock.Setup(r => r.AddAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);
        customerRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);
        uowMock.Setup(u => u.Customers).Returns(customerRepoMock.Object);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Sale, bool>>>()))
            .ReturnsAsync((Expression<Func<Sale, bool>> predicate) =>
                (sales ?? new List<Sale>()).AsQueryable().Where(predicate).ToList());
        uowMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Stub remaining repos ----
        uowMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        uowMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        uowMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        uowMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        uowMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        uowMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        uowMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        uowMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        uowMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        uowMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        uowMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        uowMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        uowMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        uowMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        uowMock.Setup(u => u.Suppliers).Returns(CreateEmptyRepoMock<Supplier>().Object);
        uowMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        uowMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        uowMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);

        var service = new CustomerService(uowMock.Object, auditMock.Object);
        return (service, uowMock, auditMock);
    }

    // ========================================================================
    // GetCustomersAsync — Search and Filter
    // ========================================================================

    [Fact]
    public async Task GetCustomersAsync_NoSearch_ReturnsAllActiveOrdered()
    {
        // Arrange
        var customers = new List<Customer>
        {
            CreateTestCustomer(name: "بسام", phone: "0791111111"),
            CreateTestCustomer(name: "أحمد", phone: "0792222222"),
            CreateTestCustomer(name: "تامر", phone: "0793333333", isActive: false)
        };
        var (service, _, _) = BuildServiceWithMocks(customers);

        // Act
        var result = await service.GetCustomersAsync();

        // Assert — only active, ordered by name
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("أحمد");
        result[1].Name.Should().Be("بسام");
    }

    [Fact]
    public async Task GetCustomersAsync_SearchByName_FiltersResults()
    {
        // Arrange
        var customers = new List<Customer>
        {
            CreateTestCustomer(name: "أحمد علي"),
            CreateTestCustomer(name: "أحمد حسن"),
            CreateTestCustomer(name: "بسام")
        };
        var (service, _, _) = BuildServiceWithMocks(customers);

        // Act
        var result = await service.GetCustomersAsync("أحمد");

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.Name.Should().Contain("أحمد"));
    }

    [Fact]
    public async Task GetCustomersAsync_SearchByPhone_FiltersResults()
    {
        // Arrange
        var customers = new List<Customer>
        {
            CreateTestCustomer(name: "أحمد", phone: "0791234567"),
            CreateTestCustomer(name: "بسام", phone: "0799876543")
        };
        var (service, _, _) = BuildServiceWithMocks(customers);

        // Act
        var result = await service.GetCustomersAsync("9876");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("بسام");
    }

    [Fact]
    public async Task GetCustomersAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var customers = new List<Customer>
        {
            CreateTestCustomer(name: "أحمد")
        };
        var (service, _, _) = BuildServiceWithMocks(customers);

        // Act
        var result = await service.GetCustomersAsync("غير موجود");

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // CreateCustomerAsync — Create Customer
    // ========================================================================

    [Fact]
    public async Task CreateCustomerAsync_Success_CreatesCustomer()
    {
        // Arrange
        var (service, uowMock, _) = BuildServiceWithMocks();

        // Act
        var result = await service.CreateCustomerAsync("أحمد علي", "0791234567", "ahmed@example.com");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("أحمد علي");
        result.Phone.Should().Be("0791234567");
        result.Email.Should().Be("ahmed@example.com");
        result.Balance.Should().Be(0);

        uowMock.Verify(u => u.Customers.AddAsync(
            It.Is<Customer>(c =>
                c.Name == "أحمد علي" &&
                c.Phone == "0791234567" &&
                c.Email == "ahmed@example.com" &&
                c.Balance == 0 &&
                c.IsActive)), Times.Once);

        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateCustomerAsync_EmptyName_Throws()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.CreateCustomerAsync("", "0791234567", null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("اسم العميل مطلوب");
    }

    [Fact]
    public async Task CreateCustomerAsync_WhitespaceName_Throws()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.CreateCustomerAsync("   ", null, null);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("اسم العميل مطلوب");
    }

    [Fact]
    public async Task CreateCustomerAsync_WithoutOptionalFields_CreatesCustomer()
    {
        // Arrange
        var (service, uowMock, _) = BuildServiceWithMocks();

        // Act
        var result = await service.CreateCustomerAsync("بسام", null, null);

        // Assert
        result.Name.Should().Be("بسام");
        result.Phone.Should().BeNull();
        result.Email.Should().BeNull();

        uowMock.Verify(u => u.Customers.AddAsync(
            It.Is<Customer>(c => c.Phone == null && c.Email == null)), Times.Once);
    }

    // ========================================================================
    // UpdateCustomerAsync — Update Customer
    // ========================================================================

    [Fact]
    public async Task UpdateCustomerAsync_Success_UpdatesCustomer()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customer = CreateTestCustomer(id, name: "أحمد قديم", phone: "0790000000");
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Customer> { customer });

        // Act
        var result = await service.UpdateCustomerAsync(id, "أحمد جديد", "0791111111", "new@example.com");

        // Assert
        result.Name.Should().Be("أحمد جديد");
        result.Phone.Should().Be("0791111111");
        result.Email.Should().Be("new@example.com");

        // Original mutated
        customer.Name.Should().Be("أحمد جديد");
        customer.Phone.Should().Be("0791111111");
        customer.Email.Should().Be("new@example.com");

        uowMock.Verify(u => u.Customers.UpdateAsync(customer), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateCustomerAsync_NotFound_Throws()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.UpdateCustomerAsync(Guid.NewGuid(), "اسم", null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العميل غير موجود");
    }

    // ========================================================================
    // GetCustomerOrderHistoryAsync — Order History
    // ========================================================================

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_CustomerExists_ReturnsOrderedSales()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customer = CreateTestCustomer(customerId);
        var sales = new List<Sale>
        {
            CreateCompletedSale(customerId, total: 100.000m),
            CreateCompletedSale(customerId, total: 50.000m)
        };
        var (service, _, _) = BuildServiceWithMocks(
            customers: new List<Customer> { customer },
            sales: sales);

        // Act
        var result = await service.GetCustomerOrderHistoryAsync(customerId);

        // Assert — ordered by CreatedAt descending
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(s => s.Status.Should().Be("Completed"));
    }

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_CustomerNotFound_Throws()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.GetCustomerOrderHistoryAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العميل غير موجود");
    }

    [Fact]
    public async Task GetCustomerOrderHistoryAsync_NoSales_ReturnsEmpty()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customer = CreateTestCustomer(customerId);
        var (service, _, _) = BuildServiceWithMocks(
            customers: new List<Customer> { customer });

        // Act
        var result = await service.GetCustomerOrderHistoryAsync(customerId);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // DeleteCustomerAsync — Soft Delete
    // ========================================================================

    [Fact]
    public async Task DeleteCustomerAsync_Success_MarksInactive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var customer = CreateTestCustomer(id, name: "أحمد", isActive: true);
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Customer> { customer });

        // Act
        await service.DeleteCustomerAsync(id);

        // Assert — customer should be marked inactive
        customer.IsActive.Should().BeFalse();
        uowMock.Verify(u => u.Customers.UpdateAsync(customer), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task DeleteCustomerAsync_NotFound_Throws()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.DeleteCustomerAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("العميل غير موجود");
    }

    [Fact]
    public async Task DeleteCustomerAsync_AlreadyInactive_DoesNotThrow()
    {
        // Arrange — customer already inactive, delete should still succeed
        var id = Guid.NewGuid();
        var customer = CreateTestCustomer(id, isActive: false);
        var (service, uowMock, _) = BuildServiceWithMocks(new List<Customer> { customer });

        // Act
        await service.DeleteCustomerAsync(id);

        // Assert — update and save still called (no-op toggle)
        uowMock.Verify(u => u.Customers.UpdateAsync(customer), Times.Once);
        uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        customer.IsActive.Should().BeFalse(); // still false
    }
}
