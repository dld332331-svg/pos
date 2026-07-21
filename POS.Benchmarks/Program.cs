using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using POS.Benchmarks.Benchmarks;

Console.WriteLine("=== POS Performance Benchmarks ===");
Console.WriteLine($"Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"Runtime: {Environment.Version}");
Console.WriteLine();
Console.WriteLine("NOTE: Run in Release mode for accurate measurements:");
Console.WriteLine("  dotnet run -c Release --project POS.Benchmarks");
Console.WriteLine();

// Run SaleService benchmarks
var paymentSummary = BenchmarkRunner.Run<SaleServiceBenchmarks>(
    ManualConfig.Create(DefaultConfig.Instance)
        .WithOption(ConfigOptions.DisableOptimizationsValidator, true));

Console.WriteLine();
Console.WriteLine("============================================");
Console.WriteLine();

// Run ESCPOS printer benchmarks
var printerSummary = BenchmarkRunner.Run<ESCPOSPrinterBenchmarks>(
    ManualConfig.Create(DefaultConfig.Instance)
        .WithOption(ConfigOptions.DisableOptimizationsValidator, true));

Console.WriteLine();
Console.WriteLine("=== All Benchmarks Complete ===");
