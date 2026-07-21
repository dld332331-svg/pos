using System.Linq.Expressions;
using Xunit;
using Moq;
using FluentAssertions;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Application.Services.Implementations;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for SupplierService — CRUD, search/filter, and purchase order history.
///
/// Test areas:
///   1. GetSuppliersAsync — all, search by name/phone/email/contact, empty results
///   2. CreateSupplierAsync — success, duplicate name, empty name
///   3. UpdateSupplierAsync — success with audit before/after, supplier not found
///   4. GetSupplierOrdersAsync — success with PO items, supplier not found
/// </summary>
public class SupplierServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultSupplierId = Guid.NewGuid();

    private static Supplier CreateSupplier(
        Guid? id = null,
        string name = "مورد ABC",
        string? contactPerson = "أحمد محمد",
        string? phone = "0791234567",
        string? email = "ahmad@example.com",
        string? address = "عمان, الأردن",
        decimal balance = 0m,
        bool isActive = true)
    {
        return new Supplier
        {
            Id = id ?? DefaultSupplierId,
            Name = name,
            ArabicName = name,
            ContactPerson = contactPerson,
            Phone = phone,
            Email = email,
            Address = address,
            Balance = balance,
            IsActive = isActive
        };
    }

    private static PurchaseOrder CreatePurchaseOrder(
        Guid? id = null,
        Guid? supplierId = null,
        string orderNumber = "PO-001",
        string status = "Pending",
        decimal totalAmount = 150.000m,
        DateTime? createdAt = null)
    {
        return new PurchaseOrder
        {
            Id = id ?? Guid.NewGuid(),
            SupplierId = supplierId ?? DefaultSupplierId,
            OrderNumber = orderNumber,
            Status = status,
            TotalAmount = totalAmount,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UserId = Guid.NewGuid()
        };
    }

    // ========================================================================
    // Mock Builder
    // ========================================================================

    private static Mock<IRepository<T>> CreateEmptyRepoMock<T>() where T : BaseEntity
    {
        var mock = new Mock<IRepository<T>>();
        mock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<T, bool>>>()))
            .ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a SupplierService with fully mocked dependencies.
    /// </summary>
    private (SupplierService service,
             Mock<IUnitOfWork> unitOfWorkMock,
             Mock<IAuditService> auditMock)
        BuildServiceWithMocks(
            List<Supplier>? suppliers = null,
            Supplier? singleSupplier = null,
            List<PurchaseOrder>? purchaseOrders = null,
            List<PurchaseOrderItem>? poItems = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditMock = new Mock<IAuditService>();

        // ---- Audit (fire-and-forget) ----
        auditMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        // ---- SaveChanges ----
        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Suppliers ----
        var supplierRepoMock = new Mock<IRepository<Supplier>>();
        supplierRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(suppliers ?? new List<Supplier>());
        supplierRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(singleSupplier);
        supplierRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Supplier, bool>>>()))
            .ReturnsAsync((Expression<Func<Supplier, bool>> predicate) =>
            {
                // FindAsync is used for duplicate name check: returns suppliers matching predicate.
                // If we have a singleSupplier, check the predicate against it.
                if (singleSupplier is not null && predicate.Compile()(singleSupplier))
                    return new List<Supplier> { singleSupplier };
                return new List<Supplier>();
            });
        supplierRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Supplier>()))
            .Returns(Task.CompletedTask);
        supplierRepoMock
            .Setup(r => r.UpdateAsync(It.IsAny<Supplier>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Suppliers).Returns(supplierRepoMock.Object);

        // ---- PurchaseOrders ----
        var poRepoMock = new Mock<IRepository<PurchaseOrder>>();
        poRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(purchaseOrders ?? new List<PurchaseOrder>());
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(poRepoMock.Object);

        // ---- PurchaseOrderItems ----
        var poItemRepoMock = new Mock<IRepository<PurchaseOrderItem>>();
        poItemRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<PurchaseOrderItem, bool>>>()))
            .ReturnsAsync(poItems ?? new List<PurchaseOrderItem>());
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(poItemRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Users).Returns(CreateEmptyRepoMock<User>().Object);
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Payments).Returns(CreateEmptyRepoMock<Payment>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(CreateEmptyRepoMock<Shift>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Expenses).Returns(CreateEmptyRepoMock<Expense>().Object);
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(CreateEmptyRepoMock<WithdrawalDeposit>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.Registers).Returns(CreateEmptyRepoMock<Register>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.InventoryBatches).Returns(CreateEmptyRepoMock<InventoryBatch>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Returns).Returns(CreateEmptyRepoMock<Return>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);
        unitOfWorkMock.Setup(u => u.Sales).Returns(CreateEmptyRepoMock<Sale>().Object);
        unitOfWorkMock.Setup(u => u.SalePromotions).Returns(CreateEmptyRepoMock<SalePromotion>().Object);
        unitOfWorkMock.Setup(u => u.Promotions).Returns(CreateEmptyRepoMock<Promotion>().Object);

        var service = new SupplierService(unitOfWorkMock.Object, auditMock.Object);

        return (service, unitOfWorkMock, auditMock);
    }

    // ========================================================================
    // GetSuppliersAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetSuppliersAsync_NoSearch_ReturnsAllOrderedByName()
    {
        // Arrange
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد ب"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد أ"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد ج")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        // Act
        var result = await service.GetSuppliersAsync();

        // Assert — ordered by name ascending
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("مورد أ");
        result[1].Name.Should().Be("مورد ب");
        result[2].Name.Should().Be("مورد ج");
    }

    [Fact]
    public async Task GetSuppliersAsync_SearchByName_ReturnsMatching()
    {
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد البركة"),
            CreateSupplier(id: Guid.NewGuid(), name: "شركة النور"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد البركة الثاني")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        var result = await service.GetSuppliersAsync(search: "البركة");

        result.Should().HaveCount(2);
        result.Should().Contain(s => s.Name == "مورد البركة");
        result.Should().Contain(s => s.Name == "مورد البركة الثاني");
    }

    [Fact]
    public async Task GetSuppliersAsync_SearchByPhone_ReturnsMatching()
    {
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد أ", phone: "0791111111"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد ب", phone: "0792222222")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        var result = await service.GetSuppliersAsync(search: "2222");

        result.Should().HaveCount(1);
        result[0].Phone.Should().Be("0792222222");
    }

    [Fact]
    public async Task GetSuppliersAsync_SearchByEmail_ReturnsMatching()
    {
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد أ", email: "a@test.com"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد ب", email: "b@test.com")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        var result = await service.GetSuppliersAsync(search: "b@test");

        result.Should().HaveCount(1);
        result[0].Email.Should().Be("b@test.com");
    }

    [Fact]
    public async Task GetSuppliersAsync_SearchByContactPerson_ReturnsMatching()
    {
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد أ", contactPerson: "خالد"),
            CreateSupplier(id: Guid.NewGuid(), name: "مورد ب", contactPerson: "سامر")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        var result = await service.GetSuppliersAsync(search: "خالد");

        result.Should().HaveCount(1);
        result[0].ContactPerson.Should().Be("خالد");
    }

    [Fact]
    public async Task GetSuppliersAsync_SearchNoMatch_ReturnsEmpty()
    {
        var suppliers = new List<Supplier>
        {
            CreateSupplier(id: Guid.NewGuid(), name: "مورد أ")
        };

        var (service, _, _) = BuildServiceWithMocks(suppliers: suppliers);

        var result = await service.GetSuppliersAsync(search: "غير موجود");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSuppliersAsync_EmptyList_ReturnsEmpty()
    {
        var (service, _, _) = BuildServiceWithMocks(suppliers: new List<Supplier>());

        var result = await service.GetSuppliersAsync();

        result.Should().BeEmpty();
    }

    // ========================================================================
    // CreateSupplierAsync Tests
    // ========================================================================

    [Fact]
    public async Task CreateSupplierAsync_ValidRequest_ReturnsCreatedSupplier()
    {
        // Arrange — singleSupplier is null so duplicate check returns empty
        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks(singleSupplier: null);

        // Act
        var result = await service.CreateSupplierAsync(
            "مورد الجودة", "محمد", "0793333333", "moh@test.com", "إربد");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("مورد الجودة");
        result.ContactPerson.Should().Be("محمد");
        result.Phone.Should().Be("0793333333");
        result.Email.Should().Be("moh@test.com");
        result.Address.Should().Be("إربد");
        result.Balance.Should().Be(0m);
        result.IsActive.Should().BeTrue();

        // Supplier was added
        unitOfWorkMock.Verify(u => u.Suppliers.AddAsync(
            It.Is<Supplier>(s =>
                s.Name == "مورد الجودة" &&
                s.Phone == "0793333333" &&
                s.IsActive)), Times.Once);

        // Audit was logged
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Supplier",
            It.IsAny<Guid>(), null,
            It.Is<string>(val => val.Contains("مورد الجودة") && val.Contains("0793333333")),
            null), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateSupplierAsync_EmptyName_ThrowsArgumentException()
    {
        var (service, _, _) = BuildServiceWithMocks();

        var act = () => service.CreateSupplierAsync(
            "  ", "محمد", null, null, null);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("اسم المورد مطلوب");
    }

    [Fact]
    public async Task CreateSupplierAsync_DuplicateName_ThrowsInvalidOperationException()
    {
        // Arrange — singleSupplier exists, so the duplicate check will find it
        var existing = CreateSupplier(name: "مورد موجود");
        var (service, _, _) = BuildServiceWithMocks(singleSupplier: existing);

        // Act
        var act = () => service.CreateSupplierAsync(
            "مورد موجود", null, null, null, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("يوجد مورد آخر بنفس الاسم");
    }

    // ========================================================================
    // UpdateSupplierAsync Tests
    // ========================================================================

    [Fact]
    public async Task UpdateSupplierAsync_ValidRequest_ReturnsUpdatedSupplier()
    {
        // Arrange
        var existing = CreateSupplier(
            name: "الاسم القديم",
            contactPerson: "الشخص القديم",
            phone: "0790000000",
            email: "old@test.com",
            address: "العنوان القديم",
            balance: 50.000m);

        var (service, unitOfWorkMock, auditMock) = BuildServiceWithMocks(
            singleSupplier: existing);

        // Act
        var result = await service.UpdateSupplierAsync(
            DefaultSupplierId,
            "الاسم الجديد", "الشخص الجديد", "0799999999",
            "new@test.com", "العنوان الجديد");

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("الاسم الجديد");
        result.ContactPerson.Should().Be("الشخص الجديد");
        result.Phone.Should().Be("0799999999");
        result.Email.Should().Be("new@test.com");
        result.Address.Should().Be("العنوان الجديد");
        result.IsActive.Should().BeTrue();

        // In-memory mutation
        existing.Name.Should().Be("الاسم الجديد");
        existing.ContactPerson.Should().Be("الشخص الجديد");

        // UpdateAsync was called
        unitOfWorkMock.Verify(u => u.Suppliers.UpdateAsync(
            It.Is<Supplier>(s => s.Name == "الاسم الجديد")), Times.Once);

        // Audit logged with before and after values
        auditMock.Verify(a => a.LogAsync(
            null, AuditActionType.SettingChanged, "Supplier",
            DefaultSupplierId,
            It.Is<string>(before => before.Contains("الاسم القديم") && before.Contains("0790000000")),
            It.Is<string>(after => after.Contains("الاسم الجديد") && after.Contains("0799999999")),
            null), Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateSupplierAsync_SupplierNotFound_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks(singleSupplier: null);

        var act = () => service.UpdateSupplierAsync(
            Guid.NewGuid(), "اسم", null, null, null, null);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المورد غير موجود");
    }

    // ========================================================================
    // GetSupplierOrdersAsync Tests
    // ========================================================================

    [Fact]
    public async Task GetSupplierOrdersAsync_ExistingSupplier_ReturnsOrders()
    {
        // Arrange
        var supplier = CreateSupplier(name: "مورد الطلبات");
        var po1 = CreatePurchaseOrder(supplierId: DefaultSupplierId, orderNumber: "PO-001", totalAmount: 100.000m);
        var po2 = CreatePurchaseOrder(supplierId: DefaultSupplierId, orderNumber: "PO-002", totalAmount: 200.000m,
            createdAt: DateTime.UtcNow.AddHours(-1));

        var poItems = new List<PurchaseOrderItem>
        {
            new()
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = po1.Id,
                InventoryItemId = Guid.NewGuid(),
                ItemName = "مادة أ",
                Quantity = 10m,
                UnitCost = 10.000m,
                TotalCost = 100.000m,
                ReceivedQuantity = 0m
            }
        };

        var (service, _, _) = BuildServiceWithMocks(
            singleSupplier: supplier,
            purchaseOrders: new List<PurchaseOrder> { po2, po1 }, // unsorted input
            poItems: poItems);

        // Act
        var result = await service.GetSupplierOrdersAsync(DefaultSupplierId);

        // Assert — ordered by CreatedAt descending
        result.Should().HaveCount(2);
        result[0].OrderNumber.Should().Be("PO-001"); // newest first
        result[1].OrderNumber.Should().Be("PO-002");

        // Supplier name mapped
        result[0].SupplierName.Should().Be("مورد الطلبات");

        // PO items mapped
        result[0].Items.Should().HaveCount(1);
        result[0].Items[0].ItemName.Should().Be("مادة أ");
        result[0].Items[0].Quantity.Should().Be(10m);
    }

    [Fact]
    public async Task GetSupplierOrdersAsync_SupplierNotFound_ThrowsInvalidOperationException()
    {
        var (service, _, _) = BuildServiceWithMocks(singleSupplier: null);

        var act = () => service.GetSupplierOrdersAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("المورد غير موجود");
    }

    [Fact]
    public async Task GetSupplierOrdersAsync_NoOrders_ReturnsEmpty()
    {
        var supplier = CreateSupplier();
        var (service, _, _) = BuildServiceWithMocks(
            singleSupplier: supplier,
            purchaseOrders: new List<PurchaseOrder>());

        var result = await service.GetSupplierOrdersAsync(DefaultSupplierId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSupplierOrdersAsync_OrdersFromOtherSupplier_NotIncluded()
    {
        // Arrange
        var supplier = CreateSupplier();
        var otherSupplierId = Guid.NewGuid();
        var otherPO = CreatePurchaseOrder(supplierId: otherSupplierId, orderNumber: "PO-OTHER");

        var (service, _, _) = BuildServiceWithMocks(
            singleSupplier: supplier,
            purchaseOrders: new List<PurchaseOrder> { otherPO });

        // Act — only this supplier's orders
        var result = await service.GetSupplierOrdersAsync(DefaultSupplierId);

        // Assert — other supplier's PO is not included
        result.Should().BeEmpty();
    }
}
