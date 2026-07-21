#nullable enable

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
/// Unit tests for ShiftService covering all 6 public methods:
/// OpenShiftAsync, CloseShiftAsync, GetCurrentShiftAsync, GetShiftHistoryAsync,
/// GetShiftSummaryAsync, GetCashReportAsync.
/// </summary>
public class ShiftServiceTests
{
    // ========================================================================
    // Test Data Builders
    // ========================================================================

    private static readonly Guid DefaultRegisterId = Guid.NewGuid();

    private static Shift CreateOpenShift(
        Guid? shiftId = null,
        Guid? userId = null,
        Guid? registerId = null,
        int shiftNumber = 1,
        decimal openingCash = 500.000m)
    {
        return new Shift
        {
            Id = shiftId ?? Guid.NewGuid(),
            ShiftNumber = shiftNumber,
            UserId = userId ?? Guid.NewGuid(),
            RegisterId = registerId ?? DefaultRegisterId,
            OpeningCash = openingCash,
            TotalSales = 0,
            TotalReturns = 0,
            TotalExpenses = 0,
            TotalDeposits = 0,
            TotalWithdrawals = 0,
            Status = ShiftStatus.Open,
            OpenedAt = DateTime.UtcNow.AddHours(-8)
        };
    }

    private static Register CreateTestRegister(Guid? registerId = null, string name = "Main Register")
    {
        return new Register
        {
            Id = registerId ?? DefaultRegisterId,
            Name = name,
            IsActive = true
        };
    }

    private static User CreateTestUser(Guid userId, string fullName = "مستخدم")
    {
        return new User
        {
            Id = userId,
            FullName = fullName,
            Username = "user",
            Role = UserRole.Cashier
        };
    }

    private static Sale CreateCompletedSale(Guid saleId, Guid shiftId, decimal total = 100.000m)
    {
        return new Sale
        {
            Id = saleId,
            ShiftId = shiftId,
            InvoiceNumber = $"INV-{saleId:N}",
            TotalAmount = total,
            Status = SaleStatus.Completed,
            IsPaid = true
        };
    }

    private static Payment CreateCashPayment(Guid saleId, decimal amount, Guid? paymentId = null)
    {
        return new Payment
        {
            Id = paymentId ?? Guid.NewGuid(),
            SaleId = saleId,
            PaymentMethod = PaymentMethod.Cash,
            Amount = amount
        };
    }

    private static Payment CreateCardPayment(Guid saleId, decimal amount)
    {
        return new Payment
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            PaymentMethod = PaymentMethod.Card,
            Amount = amount
        };
    }

    private static Expense CreateExpense(Guid shiftId, decimal amount)
    {
        return new Expense
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Amount = amount
        };
    }

    private static WithdrawalDeposit CreateWithdrawal(Guid shiftId, decimal amount)
    {
        return new WithdrawalDeposit
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Type = WithdrawalDepositType.Withdrawal,
            Amount = amount
        };
    }

    private static WithdrawalDeposit CreateDeposit(Guid shiftId, decimal amount)
    {
        return new WithdrawalDeposit
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            Type = WithdrawalDepositType.Deposit,
            Amount = amount
        };
    }

    private static Return CreateReturn(Guid saleId, decimal totalAmount)
    {
        return new Return
        {
            Id = Guid.NewGuid(),
            OriginalSaleId = saleId,
            TotalAmount = totalAmount,
            Status = "Processed"
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
        mock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<T>());
        return mock;
    }

    /// <summary>
    /// Builds a ShiftService with fully mocked IUnitOfWork and IAuditService.
    /// </summary>
    private (ShiftService service, Mock<IUnitOfWork> unitOfWorkMock, Mock<IAuditService> auditServiceMock)
        BuildServiceWithMocks(
            List<Shift>? shifts = null,
            List<Register>? registers = null,
            List<User>? users = null,
            List<Sale>? sales = null,
            List<Payment>? payments = null,
            List<Expense>? expenses = null,
            List<WithdrawalDeposit>? withdrawalsDeposits = null,
            List<Return>? returns = null)
    {
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var auditServiceMock = new Mock<IAuditService>();

        auditServiceMock
            .Setup(a => a.LogAsync(
                It.IsAny<Guid?>(), It.IsAny<AuditActionType>(),
                It.IsAny<string>(), It.IsAny<Guid?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        unitOfWorkMock.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // ---- Shifts repository ----
        var shiftRepoMock = new Mock<IRepository<Shift>>();
        shiftRepoMock
            .Setup(r => r.GetAllAsync())
            .ReturnsAsync(shifts ?? new List<Shift>());
        shiftRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => shifts?.FirstOrDefault(s => s.Id == id));
        shiftRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Shift, bool>>>()))
            .ReturnsAsync((Expression<Func<Shift, bool>> predicate) =>
                (shifts ?? new List<Shift>()).AsQueryable().Where(predicate).ToList());
        shiftRepoMock.Setup(r => r.AddAsync(It.IsAny<Shift>())).Returns(Task.CompletedTask);
        shiftRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Shift>())).Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(u => u.Shifts).Returns(shiftRepoMock.Object);

        // ---- Registers repository ----
        var registerRepoMock = new Mock<IRepository<Register>>();
        registerRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => registers?.FirstOrDefault(r => r.Id == id));
        unitOfWorkMock.Setup(u => u.Registers).Returns(registerRepoMock.Object);

        // ---- Users repository ----
        var userRepoMock = new Mock<IRepository<User>>();
        userRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid id) => users?.FirstOrDefault(u => u.Id == id));
        unitOfWorkMock.Setup(u => u.Users).Returns(userRepoMock.Object);

        // ---- Sales repository ----
        var saleRepoMock = new Mock<IRepository<Sale>>();
        saleRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Sale, bool>>>()))
            .ReturnsAsync((Expression<Func<Sale, bool>> predicate) =>
                (sales ?? new List<Sale>()).AsQueryable().Where(predicate).ToList());
        unitOfWorkMock.Setup(u => u.Sales).Returns(saleRepoMock.Object);

        // ---- Payments repository ----
        var paymentRepoMock = new Mock<IRepository<Payment>>();
        paymentRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Payment, bool>>>()))
            .ReturnsAsync((Expression<Func<Payment, bool>> predicate) =>
                (payments ?? new List<Payment>()).AsQueryable().Where(predicate).ToList());
        unitOfWorkMock.Setup(u => u.Payments).Returns(paymentRepoMock.Object);

        // ---- Expenses repository ----
        var expenseRepoMock = new Mock<IRepository<Expense>>();
        expenseRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Expense, bool>>>()))
            .ReturnsAsync((Expression<Func<Expense, bool>> predicate) =>
                (expenses ?? new List<Expense>()).AsQueryable().Where(predicate).ToList());
        unitOfWorkMock.Setup(u => u.Expenses).Returns(expenseRepoMock.Object);

        // ---- WithdrawalDeposits repository ----
        var wdRepoMock = new Mock<IRepository<WithdrawalDeposit>>();
        wdRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<WithdrawalDeposit, bool>>>()))
            .ReturnsAsync((Expression<Func<WithdrawalDeposit, bool>> predicate) =>
                (withdrawalsDeposits ?? new List<WithdrawalDeposit>()).AsQueryable().Where(predicate).ToList());
        unitOfWorkMock.Setup(u => u.WithdrawalDeposits).Returns(wdRepoMock.Object);

        // ---- Returns repository ----
        var returnRepoMock = new Mock<IRepository<Return>>();
        returnRepoMock
            .Setup(r => r.FindAsync(It.IsAny<Expression<Func<Return, bool>>>()))
            .ReturnsAsync((Expression<Func<Return, bool>> predicate) =>
                (returns ?? new List<Return>()).AsQueryable().Where(predicate).ToList());
        unitOfWorkMock.Setup(u => u.Returns).Returns(returnRepoMock.Object);

        // ---- Stub remaining repos ----
        unitOfWorkMock.Setup(u => u.Products).Returns(CreateEmptyRepoMock<Product>().Object);
        unitOfWorkMock.Setup(u => u.Categories).Returns(CreateEmptyRepoMock<Category>().Object);
        unitOfWorkMock.Setup(u => u.InventoryItems).Returns(CreateEmptyRepoMock<InventoryItem>().Object);
        unitOfWorkMock.Setup(u => u.InventoryMovements).Returns(CreateEmptyRepoMock<InventoryMovement>().Object);
        unitOfWorkMock.Setup(u => u.SaleItems).Returns(CreateEmptyRepoMock<SaleItem>().Object);
        unitOfWorkMock.Setup(u => u.SaleItemModifiers).Returns(CreateEmptyRepoMock<SaleItemModifier>().Object);
        unitOfWorkMock.Setup(u => u.Settings).Returns(CreateEmptyRepoMock<Setting>().Object);
        unitOfWorkMock.Setup(u => u.Tables).Returns(CreateEmptyRepoMock<Table>().Object);
        unitOfWorkMock.Setup(u => u.Customers).Returns(CreateEmptyRepoMock<Customer>().Object);
        unitOfWorkMock.Setup(u => u.HeldSales).Returns(CreateEmptyRepoMock<HeldSale>().Object);
        unitOfWorkMock.Setup(u => u.Printers).Returns(CreateEmptyRepoMock<Printer>().Object);
        unitOfWorkMock.Setup(u => u.KitchenStations).Returns(CreateEmptyRepoMock<KitchenStation>().Object);
        unitOfWorkMock.Setup(u => u.Rooms).Returns(CreateEmptyRepoMock<Room>().Object);
        unitOfWorkMock.Setup(u => u.ModifierGroups).Returns(CreateEmptyRepoMock<ModifierGroup>().Object);
        unitOfWorkMock.Setup(u => u.Modifiers).Returns(CreateEmptyRepoMock<Modifier>().Object);
        unitOfWorkMock.Setup(u => u.ModifierSizes).Returns(CreateEmptyRepoMock<ModifierSize>().Object);
        unitOfWorkMock.Setup(u => u.Recipes).Returns(CreateEmptyRepoMock<Recipe>().Object);
        unitOfWorkMock.Setup(u => u.RecipeIngredients).Returns(CreateEmptyRepoMock<RecipeIngredient>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrders).Returns(CreateEmptyRepoMock<PurchaseOrder>().Object);
        unitOfWorkMock.Setup(u => u.PurchaseOrderItems).Returns(CreateEmptyRepoMock<PurchaseOrderItem>().Object);
        unitOfWorkMock.Setup(u => u.ReturnItems).Returns(CreateEmptyRepoMock<ReturnItem>().Object);

        var service = new ShiftService(unitOfWorkMock.Object, auditServiceMock.Object);
        return (service, unitOfWorkMock, auditServiceMock);
    }

    // ========================================================================
    // OpenShiftAsync — Open Shift
    // ========================================================================

    [Fact]
    public async Task OpenShiftAsync_HappyPath_OpensShiftWithNextNumber()
    {
        // Arrange — one existing shift for this register
        var userId = Guid.NewGuid();
        var register = CreateTestRegister();

        var existingShift = CreateOpenShift(
            shiftNumber: 5,
            userId: Guid.NewGuid(), // different user
            registerId: DefaultRegisterId);

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            shifts: new List<Shift> { existingShift },
            registers: new List<Register> { register },
            users: new List<User> { CreateTestUser(userId) });

        var request = new OpenShiftRequest(OpeningCash: 500.000m, RegisterId: DefaultRegisterId);

        // Act
        var result = await service.OpenShiftAsync(request, userId);

        // Assert — returned DTO
        result.Should().NotBeNull();
        result.ShiftNumber.Should().Be(6); // max(5) + 1
        result.OpeningCash.Should().Be(500.000m);
        result.Status.Should().Be("Open");
        result.RegisterName.Should().Be("Main Register");

        // Shift was added with correct properties
        unitOfWorkMock.Verify(u => u.Shifts.AddAsync(
            It.Is<Shift>(s =>
                s.ShiftNumber == 6 &&
                s.UserId == userId &&
                s.RegisterId == DefaultRegisterId &&
                s.OpeningCash == 500.000m &&
                s.Status == ShiftStatus.Open)),
            Times.Once);

        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        // Audit logged
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.ShiftOpened,
            "Shift",
            It.IsAny<Guid>(),
            null,
            "OpeningCash=500.000,Register=Main Register",
            null), Times.Once);
    }

    [Fact]
    public async Task OpenShiftAsync_ExistingOpenShift_ThrowsInvalidOperationException()
    {
        // Arrange — same user already has an open shift
        var userId = Guid.NewGuid();
        var existingOpenShift = CreateOpenShift(userId: userId, shiftNumber: 1);
        var register = CreateTestRegister();

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { existingOpenShift },
            registers: new List<Register> { register });

        var request = new OpenShiftRequest(OpeningCash: 100m, RegisterId: DefaultRegisterId);

        // Act
        var act = () => service.OpenShiftAsync(request, userId);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("يوجد وردية مفتوحة بالفعل لهذا المستخدم");
    }

    [Fact]
    public async Task OpenShiftAsync_RegisterNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — no registers in repo
        var (service, _, _) = BuildServiceWithMocks();

        var request = new OpenShiftRequest(OpeningCash: 100m, RegisterId: Guid.NewGuid());

        // Act
        var act = () => service.OpenShiftAsync(request, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الجهاز غير موجود");
    }

    [Fact]
    public async Task OpenShiftAsync_FirstShift_NumberIsOne()
    {
        // Arrange — no existing shifts
        var userId = Guid.NewGuid();
        var register = CreateTestRegister();

        var (service, unitOfWorkMock, _) = BuildServiceWithMocks(
            shifts: new List<Shift>(),
            registers: new List<Register> { register },
            users: new List<User> { CreateTestUser(userId) });

        var request = new OpenShiftRequest(OpeningCash: 300.000m, RegisterId: DefaultRegisterId);

        // Act
        var result = await service.OpenShiftAsync(request, userId);

        // Assert — first shift number is 1
        result.ShiftNumber.Should().Be(1);
        unitOfWorkMock.Verify(u => u.Shifts.AddAsync(
            It.Is<Shift>(s => s.ShiftNumber == 1)), Times.Once);
    }

    // ========================================================================
    // CloseShiftAsync — Close Shift with Cash Handover
    // ========================================================================

    [Fact]
    public async Task CloseShiftAsync_HappyPath_CalculatesVarianceAndLogsAudit()
    {
        // Arrange — shift with sales and payments
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId, userId, openingCash: 500.000m);

        var sale1Id = Guid.NewGuid();
        var sale2Id = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(sale1Id, shiftId, total: 150.000m),
            CreateCompletedSale(sale2Id, shiftId, total: 75.000m)
        };
        var payments = new List<Payment>
        {
            CreateCashPayment(sale1Id, 150.000m),
            CreateCardPayment(sale2Id, 75.000m)  // card excluded from expected cash
        };

        var (service, unitOfWorkMock, auditServiceMock) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            users: new List<User> { CreateTestUser(userId) },
            sales: sales,
            payments: payments);

        var request = new CloseShiftRequest(ShiftId: shiftId, ActualCash: 700.000m);

        // Act
        var result = await service.CloseShiftAsync(request, userId);

        // Assert — expected = 500 + 150 = 650, actual = 700, variance = 700 - 650 = 50
        result.Should().NotBeNull();
        result.TotalSales.Should().Be(225.000m);  // 150 + 75
        result.Status.Should().Be("Closed");

        shift.ExpectedCash.Should().Be(650.000m);
        shift.ActualCash.Should().Be(700.000m);
        shift.Variance.Should().Be(50.000m);
        shift.Status.Should().Be(ShiftStatus.Closed);

        unitOfWorkMock.Verify(u => u.Shifts.UpdateAsync(shift), Times.Once);
        unitOfWorkMock.Verify(u => u.SaveChangesAsync(), Times.AtLeastOnce);

        // Audit logged with before/after
        auditServiceMock.Verify(a => a.LogAsync(
            userId,
            AuditActionType.ShiftClosed,
            "Shift",
            shiftId,
            "Status=Open",
            "Status=Closed,Expected=650.000,Actual=700.000,Variance=50.000",
            null), Times.Once);
    }

    [Fact]
    public async Task CloseShiftAsync_ShiftNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — no shifts
        var (service, _, _) = BuildServiceWithMocks();

        var request = new CloseShiftRequest(ShiftId: Guid.NewGuid(), ActualCash: 100m);

        // Act
        var act = () => service.CloseShiftAsync(request, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوردية غير موجودة");
    }

    [Fact]
    public async Task CloseShiftAsync_AlreadyClosed_ThrowsInvalidOperationException()
    {
        // Arrange — shift is already closed
        var shiftId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId);
        shift.Status = ShiftStatus.Closed;

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() });

        var request = new CloseShiftRequest(ShiftId: shiftId, ActualCash: 100m);

        // Act
        var act = () => service.CloseShiftAsync(request, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوردية ليست مفتوحة");
    }

    [Fact]
    public async Task CloseShiftAsync_WithExpensesAndWithdrawals_DeductsFromExpected()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId, userId, openingCash: 1000.000m);

        var saleId = Guid.NewGuid();
        var sales = new List<Sale> { CreateCompletedSale(saleId, shiftId, total: 500.000m) };
        var payments = new List<Payment> { CreateCashPayment(saleId, 500.000m) };
        var expenses = new List<Expense> { CreateExpense(shiftId, 100.000m) };
        var wds = new List<WithdrawalDeposit>
        {
            CreateWithdrawal(shiftId, 200.000m),
            CreateDeposit(shiftId, 50.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            users: new List<User> { CreateTestUser(userId) },
            sales: sales,
            payments: payments,
            expenses: expenses,
            withdrawalsDeposits: wds);

        var request = new CloseShiftRequest(ShiftId: shiftId, ActualCash: 1200.000m);

        // Act
        var result = await service.CloseShiftAsync(request, userId);

        // Assert — expected = 1000 + 500 - 100 (expenses) - 200 (withdrawal) + 50 (deposit) = 1250
        shift.ExpectedCash.Should().Be(1250.000m);
        shift.Variance.Should().Be(-50.000m); // 1200 - 1250
        shift.TotalExpenses.Should().Be(100.000m);
        shift.TotalWithdrawals.Should().Be(200.000m);
        shift.TotalDeposits.Should().Be(50.000m);
    }

    [Fact]
    public async Task CloseShiftAsync_WithReturns_RecordsTotalReturns()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId, userId, openingCash: 500.000m);

        var saleId = Guid.NewGuid();
        var sales = new List<Sale> { CreateCompletedSale(saleId, shiftId, total: 100.000m) };
        var payments = new List<Payment> { CreateCashPayment(saleId, 100.000m) };
        var returns = new List<Return>
        {
            CreateReturn(saleId, 20.000m),
            CreateReturn(saleId, 15.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            users: new List<User> { CreateTestUser(userId) },
            sales: sales,
            payments: payments,
            returns: returns);

        var request = new CloseShiftRequest(ShiftId: shiftId, ActualCash: 600.000m);

        // Act
        var result = await service.CloseShiftAsync(request, userId);

        // Assert — returns don't affect expected cash (expected = 500 + 100 = 600)
        shift.ExpectedCash.Should().Be(600.000m);
        shift.TotalReturns.Should().Be(35.000m);
    }

    // ========================================================================
    // GetCurrentShiftAsync — Current Open Shift Lookup
    // ========================================================================

    [Fact]
    public async Task GetCurrentShiftAsync_OpenShiftExists_ReturnsDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var shift = CreateOpenShift(userId: userId);
        var register = CreateTestRegister();
        var user = CreateTestUser(userId);

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { register },
            users: new List<User> { user });

        // Act
        var result = await service.GetCurrentShiftAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result!.ShiftNumber.Should().Be(shift.ShiftNumber);
        result.Status.Should().Be("Open");
        result.UserName.Should().Be("مستخدم");
        result.RegisterName.Should().Be("Main Register");
    }

    [Fact]
    public async Task GetCurrentShiftAsync_NoOpenShift_ReturnsNull()
    {
        // Arrange — no shifts
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetCurrentShiftAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    // ========================================================================
    // GetShiftHistoryAsync — Shift History
    // ========================================================================

    [Fact]
    public async Task GetShiftHistoryAsync_NoFilter_ReturnsAllOrderedDescending()
    {
        // Arrange — 3 shifts
        var userId = Guid.NewGuid();
        var reg = CreateTestRegister();
        var user = CreateTestUser(userId);

        var shift1 = CreateOpenShift(shiftNumber: 1, userId: userId);
        var shift2 = CreateOpenShift(shiftNumber: 2, userId: userId);
        shift2.OpenedAt = shift1.OpenedAt.AddHours(2);
        var shift3 = CreateOpenShift(shiftNumber: 3, userId: userId);
        shift3.OpenedAt = shift2.OpenedAt.AddHours(1);

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift1, shift2, shift3 },
            registers: new List<Register> { reg },
            users: new List<User> { user });

        // Act
        var result = await service.GetShiftHistoryAsync(null, null);

        // Assert — ordered by OpenedAt descending
        result.Should().HaveCount(3);
        result[0].ShiftNumber.Should().Be(3);
        result[1].ShiftNumber.Should().Be(2);
        result[2].ShiftNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetShiftHistoryAsync_DateRange_FiltersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var reg = CreateTestRegister();
        var user = CreateTestUser(userId);

        var shift1 = CreateOpenShift(shiftNumber: 1, userId: userId);
        shift1.OpenedAt = new DateTime(2026, 7, 18, 8, 0, 0, DateTimeKind.Utc);
        var shift2 = CreateOpenShift(shiftNumber: 2, userId: userId);
        shift2.OpenedAt = new DateTime(2026, 7, 19, 8, 0, 0, DateTimeKind.Utc);
        var shift3 = CreateOpenShift(shiftNumber: 3, userId: userId);
        shift3.OpenedAt = new DateTime(2026, 7, 20, 8, 0, 0, DateTimeKind.Utc);

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift1, shift2, shift3 },
            registers: new List<Register> { reg },
            users: new List<User> { user });

        // Act — filter to 19th only (from 19th 00:00 to 19th 23:59)
        var from = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 7, 19, 0, 0, 0, DateTimeKind.Utc);
        var result = await service.GetShiftHistoryAsync(from, to);

        // Assert — only shift2 (July 19) should be included
        result.Should().HaveCount(1);
        result[0].ShiftNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetShiftHistoryAsync_Empty_ReturnsEmptyList()
    {
        // Arrange — no shifts
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var result = await service.GetShiftHistoryAsync(null, null);

        // Assert
        result.Should().BeEmpty();
    }

    // ========================================================================
    // GetShiftSummaryAsync — X Report
    // ========================================================================

    [Fact]
    public async Task GetShiftSummaryAsync_HappyPath_ReturnsSummary()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId);

        var sale1Id = Guid.NewGuid();
        var sale2Id = Guid.NewGuid();
        var sales = new List<Sale>
        {
            CreateCompletedSale(sale1Id, shiftId, total: 100.000m),
            CreateCompletedSale(sale2Id, shiftId, total: 200.000m)
        };
        var payments = new List<Payment>
        {
            CreateCashPayment(sale1Id, 100.000m),
            CreateCardPayment(sale2Id, 200.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            sales: sales,
            payments: payments);

        // Act
        var result = await service.GetShiftSummaryAsync(shiftId);

        // Assert
        result.TotalCashSales.Should().Be(100.000m);
        result.TotalCardSales.Should().Be(200.000m);
        result.TotalSales.Should().Be(300.000m);
        result.TotalTransactions.Should().Be(2);
        result.TotalReturns.Should().Be(0);
    }

    [Fact]
    public async Task GetShiftSummaryAsync_WithReturns_CountsReturns()
    {
        // Arrange
        var shiftId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId);

        var saleId = Guid.NewGuid();
        var sales = new List<Sale> { CreateCompletedSale(saleId, shiftId, total: 100.000m) };
        var payments = new List<Payment> { CreateCashPayment(saleId, 100.000m) };
        var returns = new List<Return> { CreateReturn(saleId, 10.000m) };

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            sales: sales,
            payments: payments,
            returns: returns);

        // Act
        var result = await service.GetShiftSummaryAsync(shiftId);

        // Assert
        result.TotalCashSales.Should().Be(100.000m);
        result.TotalReturns.Should().Be(1); // count of returns, not total amount
        result.TotalTransactions.Should().Be(1);
    }

    [Fact]
    public async Task GetShiftSummaryAsync_ShiftNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.GetShiftSummaryAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوردية غير موجودة");
    }

    // ========================================================================
    // GetCashReportAsync — Z Report (Cash Report)
    // ========================================================================

    [Fact]
    public async Task GetCashReportAsync_ClosedShift_UsesStoredExpectedCash()
    {
        // Arrange — closed shift with stored ExpectedCash
        var shiftId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId, openingCash: 500.000m);
        shift.ExpectedCash = 650.000m;
        shift.ActualCash = 640.000m;
        shift.Variance = -10.000m;

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() });

        // Act
        var result = await service.GetCashReportAsync(shiftId);

        // Assert — uses stored values
        result.ExpectedCash.Should().Be(650.000m);
        result.ActualCash.Should().Be(640.000m);
        result.Variance.Should().Be(-10.000m);
    }

    [Fact]
    public async Task GetCashReportAsync_OpenShift_ComputesExpectedDynamically()
    {
        // Arrange — open shift (no ExpectedCash stored) with sales
        var shiftId = Guid.NewGuid();
        var shift = CreateOpenShift(shiftId, openingCash: 1000.000m);
        // shift.ExpectedCash is null (not yet closed)

        var saleId = Guid.NewGuid();
        var sales = new List<Sale> { CreateCompletedSale(saleId, shiftId, total: 300.000m) };
        var payments = new List<Payment>
        {
            CreateCashPayment(saleId, 200.000m),
            CreateCardPayment(saleId, 100.000m)  // card excluded
        };
        var expenses = new List<Expense> { CreateExpense(shiftId, 50.000m) };
        var wds = new List<WithdrawalDeposit>
        {
            CreateWithdrawal(shiftId, 100.000m),
            CreateDeposit(shiftId, 30.000m)
        };

        var (service, _, _) = BuildServiceWithMocks(
            shifts: new List<Shift> { shift },
            registers: new List<Register> { CreateTestRegister() },
            sales: sales,
            payments: payments,
            expenses: expenses,
            withdrawalsDeposits: wds);

        // Act
        var result = await service.GetCashReportAsync(shiftId);

        // Assert — expected = 1000 + 200 - 50 - 100 + 30 = 1080
        result.ExpectedCash.Should().Be(1080.000m);
        result.TotalCashPayments.Should().Be(200.000m);
        result.TotalCardPayments.Should().Be(100.000m);
        result.TotalExpenses.Should().Be(50.000m);
        result.TotalWithdrawals.Should().Be(100.000m);
        result.TotalDeposits.Should().Be(30.000m);
        result.ActualCash.Should().Be(0); // no actual cash for open shift
        result.Variance.Should().Be(0);   // no variance for open shift
    }

    [Fact]
    public async Task GetCashReportAsync_ShiftNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var (service, _, _) = BuildServiceWithMocks();

        // Act
        var act = () => service.GetCashReportAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("الوردية غير موجودة");
    }
}
