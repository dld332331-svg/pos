#nullable enable

using Xunit;
using FluentAssertions;
using POS.Benchmarks.Benchmarks;

namespace POS.Tests.IntegrationTests;

/// <summary>
/// Smoke tests that verify all benchmark methods execute without throwing.
///
/// These tests run each benchmark at ItemCount=1 — enough to exercise the
/// full setup + benchmark pipeline without the runtime of higher param values.
///
/// Covers both benchmark suites:
///   1. SaleServiceBenchmarks — ProcessPayment, CancelSale, HoldSale
///   2. ESCPOSPrinterBenchmarks — PrintReceipt, PrintKitchenTicket, TestPrint
/// </summary>
public sealed class SaleServiceBenchmarkSmokeTests : IDisposable
{
    private readonly SaleServiceBenchmarks _benchmarks;

    public SaleServiceBenchmarkSmokeTests()
    {
        _benchmarks = new SaleServiceBenchmarks
        {
            ItemCount = 1
        };
        _benchmarks.Setup();
    }

    [Fact]
    public async Task ProcessPayment_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.ProcessPayment();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CancelSale_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.CancelSale();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task HoldSale_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.HoldSale();

        // Assert
        await act.Should().NotThrowAsync();
    }

    public void Dispose()
    {
        _benchmarks.Cleanup();
    }
}

public sealed class ESCPOSPrinterBenchmarkSmokeTests
{
    private readonly ESCPOSPrinterBenchmarks _benchmarks;

    public ESCPOSPrinterBenchmarkSmokeTests()
    {
        _benchmarks = new ESCPOSPrinterBenchmarks
        {
            ItemCount = 1
        };
        _benchmarks.Setup();
    }

    [Fact]
    public async Task PrintReceipt_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.PrintReceipt();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PrintKitchenTicket_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.PrintKitchenTicket();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task TestPrint_DoesNotThrow()
    {
        // Act
        var act = () => _benchmarks.TestPrint();

        // Assert
        await act.Should().NotThrowAsync();
    }
}
