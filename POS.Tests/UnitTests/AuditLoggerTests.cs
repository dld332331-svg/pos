#nullable enable

using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Domain.Interfaces;
using POS.Infrastructure.Database;
using POS.Infrastructure.Security;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for <see cref="AuditLogger.LogAsync"/> covering:
/// - Happy path: audit entry created and saved
/// - IP resolution fallback (inner catch)
/// - SaveChanges failure (outer catch — audit never crashes the app)
/// Uses EF Core InMemory provider with a mocked <see cref="ILoggerService"/>.
/// </summary>
public sealed class AuditLoggerTests
{
    private static int _dbCounter;

    // ========================================================================
    // Helpers
    // ========================================================================

    /// <summary>
    /// Builds a fresh AuditLogger with InMemory database and a mocked logger.
    /// </summary>
    private (AuditLogger Logger, POSDbContext Context, Mock<ILoggerService> LoggerMock) BuildLogger(
        string? dbName = null)
    {
        dbName ??= $"AuditTest_{++_dbCounter}_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var context = new POSDbContext(options);
        var loggerMock = new Mock<ILoggerService>();
        loggerMock.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<object?[]>()));

        var logger = new AuditLogger(context, loggerMock.Object);
        return (logger, context, loggerMock);
    }

    // ========================================================================
    // Happy Path
    // ========================================================================

    [Fact]
    public async Task LogAsync_HappyPath_CreatesAuditEntry()
    {
        // Arrange
        var (logger, context, loggerMock) = BuildLogger();
        using var _ = context;
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        await logger.LogAsync(
            userId,
            AuditActionType.LoginSuccess,
            "User",
            entityId,
            null,
            null,
            "Login from test");

        // Assert — entry was saved to the database
        var entries = await context.AuditLogs.ToListAsync();
        entries.Should().HaveCount(1);

        var entry = entries[0];
        entry.UserId.Should().Be(userId);
        entry.ActionType.Should().Be(AuditActionType.LoginSuccess);
        entry.EntityName.Should().Be("User");
        entry.EntityId.Should().Be(entityId);
        entry.Reason.Should().Be("Login from test");
        entry.BeforeValue.Should().BeNull();
        entry.AfterValue.Should().BeNull();
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // IP address should be resolved (or fallback to 127.0.0.1)
        entry.IPAddress.Should().NotBeNullOrEmpty();

        // LogDebug was called with the audit message
        loggerMock.Verify(l => l.LogDebug(
            It.Is<string>(s => s.Contains("Audit:") && s.Contains("LoginSuccess")),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task LogAsync_MultipleCalls_CreatesMultipleEntries()
    {
        // Arrange
        var (logger, context, _) = BuildLogger();
        using var _ = context;

        // Act
        await logger.LogAsync(Guid.NewGuid(), AuditActionType.LoginSuccess, "User", null, null, null, "First");
        await logger.LogAsync(Guid.NewGuid(), AuditActionType.Logout, "User", null, null, null, "Second");

        // Assert
        var entries = await context.AuditLogs.ToListAsync();
        entries.Should().HaveCount(2);
        entries[0].ActionType.Should().Be(AuditActionType.LoginSuccess);
        entries[1].ActionType.Should().Be(AuditActionType.Logout);
    }

    [Fact]
    public async Task LogAsync_WithAllFields_FillsEntryCorrectly()
    {
        // Arrange
        var (logger, context, _) = BuildLogger();
        using var _ = context;
        var userId = Guid.NewGuid();
        var entityId = Guid.NewGuid();

        // Act
        await logger.LogAsync(
            userId,
            AuditActionType.ProductCreated,
            "Product",
            entityId,
            "before_value",
            "after_value",
            "Price updated");

        // Assert
        var entry = await context.AuditLogs.FirstOrDefaultAsync();
        entry.Should().NotBeNull();
        entry!.UserId.Should().Be(userId);
        entry.EntityId.Should().Be(entityId);
        entry.BeforeValue.Should().Be("before_value");
        entry.AfterValue.Should().Be("after_value");
        entry.Reason.Should().Be("Price updated");
        entry.Id.Should().NotBeEmpty();
    }

    // ========================================================================
    // Error Handling — SaveChanges Failure
    // ========================================================================

    // ========================================================================
    // Error Handling — IP Resolution Failure (Inner Catch)
    // ========================================================================

    /// <summary>
    /// A test-only subclass of <see cref="AuditLogger"/> that overrides
    /// <see cref="AuditLogger.ResolveLocalIpAddressOrThrow"/> to throw, exercising
    /// the <see cref="AuditLogger.GetLocalIpAddress"/> catch block where DNS
    /// resolution fails.
    /// </summary>
    private sealed class AuditLoggerWithBrokenDns : AuditLogger
    {
        public AuditLoggerWithBrokenDns(POSDbContext context, ILoggerService logger)
            : base(context, logger)
        {
        }

        protected override string ResolveLocalIpAddressOrThrow()
        {
            // Simulate DNS failure — base.GetLocalIpAddress's catch handles it gracefully
            throw new System.Net.Sockets.SocketException(
                (int)System.Net.Sockets.SocketError.HostNotFound);
        }
    }

    [Fact]
    public async Task LogAsync_IpResolutionFails_ShouldFallbackAndLogDebug()
    {
        // Arrange
        var dbName = $"AuditDnsFail_{Guid.NewGuid()}";
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        using var context = new POSDbContext(options);
        var loggerMock = new Mock<ILoggerService>();
        loggerMock.Setup(l => l.LogDebug(It.IsAny<string>(), It.IsAny<object?[]>()));

        var logger = new AuditLoggerWithBrokenDns(context, loggerMock.Object);

        // Act — IP resolution throws, inner catch handles it
        await logger.LogAsync(
            Guid.NewGuid(),
            AuditActionType.LoginSuccess,
            "User",
            null,
            null,
            null,
            "DNS bypass test");

        // Assert — entry was still saved (inner catch does not abort the flow)
        var entries = await context.AuditLogs.ToListAsync();
        entries.Should().HaveCount(1);
        entries[0].IPAddress.Should().Be("127.0.0.1");

        // LogDebug was called for the DNS failure
        loggerMock.Verify(l => l.LogDebug(
            It.Is<string>(s => s.Contains("Could not resolve local IP address")),
            It.IsAny<object?[]>()), Times.Once);
    }

    [Fact]
    public async Task LogAsync_SaveChangesFails_DoesNotThrowAndLogsError()
    {
        // Arrange — create a logger with a context we can dispose early
        using var context = new POSDbContext(
            new DbContextOptionsBuilder<POSDbContext>()
                .UseInMemoryDatabase($"AuditFail_{Guid.NewGuid()}")
                .Options);
        var loggerMock = new Mock<ILoggerService>();
        var logger = new AuditLogger(context, loggerMock.Object);

        // Dispose the context — subsequent SaveChangesAsync will throw
        context.Dispose();

        // Act — the outer catch should swallow the exception and log the error
        var act = () => logger.LogAsync(
            Guid.NewGuid(),
            AuditActionType.LoginSuccess,
            "User",
            null,
            null,
            null,
            "This should be logged as an error");

        // Assert — no exception bubbles up
        await act.Should().NotThrowAsync();

        // LogError was called with the failure message
        loggerMock.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Failed to write audit log") || s.Contains("disposed")),
            It.IsAny<Exception?>(),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }
}
