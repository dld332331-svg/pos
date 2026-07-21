#nullable enable

using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using POS.Domain.Interfaces;
using POS.Infrastructure.Backup;

namespace POS.Tests.UnitTests;

/// <summary>
/// Unit tests for SqlBackupExecutor covering:
/// - Constructor with connection string parsing (GetDatabaseName)
/// - Exception wrapping for unreachable SQL Server
/// - SqlException rethrow for SET SINGLE_USER/MULTI_USER
/// - Optional logger parameter
/// </summary>
/// <remarks>
/// SqlConnectionStringBuilder.InitialCatalog returns string.Empty (not null)
/// when the connection string has no Database/InitialCatalog specified, so
/// the ?? null-guard in GetDatabaseName() does NOT fire. All methods proceed
/// to attempt a connection to the SQL Server and fail with SqlException.
///
/// Tests use Connect Timeout=1 to minimize test duration on connection attempts.
/// </remarks>
public class SqlBackupExecutorTests
{
    private const string BasicConnectionString = "Server=.;Trusted_Connection=True;Connect Timeout=1;";
    private const string WithInitialCatalog = "Server=.;Database=POS_TestDB;Trusted_Connection=True;Connect Timeout=1;";

    // ========================================================================
    // Constructor — Logger Parameter
    // ========================================================================

    [Fact]
    public void Constructor_WithLogger_DoesNotThrow()
    {
        // Arrange
        var logger = new Mock<ILoggerService>().Object;

        // Act
        var act = () => new SqlBackupExecutor(WithInitialCatalog, logger);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithoutLogger_DoesNotThrow()
    {
        // Act
        var act = () => new SqlBackupExecutor(WithInitialCatalog);

        // Assert
        act.Should().NotThrow();
    }

    // ========================================================================
    // BackupDatabaseAsync — Connection Attempt
    // ========================================================================

    [Fact]
    public async Task BackupDatabaseAsync_WrapsSqlExceptionInInvalidOperationException()
    {
        // Arrange
        var executor = new SqlBackupExecutor(BasicConnectionString);

        // Act
        var act = () => executor.BackupDatabaseAsync(@"C:\backup.bak");

        // Assert — SqlException is caught and wrapped in InvalidOperationException
        // (connection string has no InitialCatalog, but InitialCatalog returns ""
        // not null, so the ?? guard doesn't fire; connection is attempted)
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Failed to backup database");
        ex.Which.InnerException.Should().BeOfType<SqlException>();
    }

    [Fact]
    public async Task BackupDatabaseAsync_WithLogger_LogsErrorOnFailure()
    {
        // Arrange
        var loggerMock = new Mock<ILoggerService>();
        var executor = new SqlBackupExecutor(BasicConnectionString, loggerMock.Object);

        // Act
        var act = () => executor.BackupDatabaseAsync(@"C:\backup.bak");

        // Assert — error was logged before wrapping
        await act.Should().ThrowAsync<InvalidOperationException>();
        loggerMock.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("BACKUP DATABASE failed")),
            It.IsAny<Exception>(),
            It.IsAny<object?[]>()), Times.AtLeastOnce);
    }

    // ========================================================================
    // BackupDatabaseAsync — With Valid Connection String Pattern
    // ========================================================================

    [Fact]
    public async Task BackupDatabaseAsync_WithInitialCatalog_WrapsSqlException()
    {
        // Arrange — connection string has InitialCatalog but server is unreachable
        var executor = new SqlBackupExecutor(WithInitialCatalog);

        // Act
        var act = () => executor.BackupDatabaseAsync(@"C:\backup.bak");

        // Assert — SqlException is caught and wrapped in InvalidOperationException
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Failed to backup database");
    }

    // ========================================================================
    // RestoreDatabaseAsync — Connection Attempt (FILELISTONLY phase)
    // ========================================================================

    [Fact]
    public async Task RestoreDatabaseAsync_WrapsSqlExceptionInInvalidOperationException()
    {
        // Arrange
        var executor = new SqlBackupExecutor(BasicConnectionString);

        // Act
        var act = () => executor.RestoreDatabaseAsync(@"C:\backup.bak");

        // Assert — SqlException from FILELISTONLY connection caught and wrapped
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Failed to read backup file information");
        ex.Which.InnerException.Should().BeOfType<SqlException>();
    }

    [Fact]
    public async Task RestoreDatabaseAsync_WithInitialCatalog_WrapsSqlException()
    {
        // Arrange
        var executor = new SqlBackupExecutor(WithInitialCatalog);

        // Act
        var act = () => executor.RestoreDatabaseAsync(@"C:\backup.bak");

        // Assert — SqlException caught and wrapped
        var ex = await act.Should().ThrowAsync<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Failed to read backup file information");
    }

    // ========================================================================
    // SetSingleUserModeAsync — SqlException Rethrown (not wrapped)
    // ========================================================================

    [Fact]
    public async Task SetSingleUserModeAsync_RethrowsSqlException()
    {
        // Arrange
        var executor = new SqlBackupExecutor(BasicConnectionString);

        // Act
        var act = () => executor.SetSingleUserModeAsync();

        // Assert — SetSingleUserModeAsync catches SqlException, logs error,
        // and rethrows the ORIGINAL SqlException (not wrapped)
        await act.Should().ThrowAsync<SqlException>();
    }

    // ========================================================================
    // SetMultiUserModeAsync — SqlException Rethrown (not wrapped)
    // ========================================================================

    [Fact]
    public async Task SetMultiUserModeAsync_RethrowsSqlException()
    {
        // Arrange
        var executor = new SqlBackupExecutor(BasicConnectionString);

        // Act
        var act = () => executor.SetMultiUserModeAsync();

        // Assert — SetMultiUserModeAsync rethrows the original SqlException
        await act.Should().ThrowAsync<SqlException>();
    }
}
