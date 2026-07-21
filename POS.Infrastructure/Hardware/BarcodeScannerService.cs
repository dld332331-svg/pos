using System.IO.Ports;
using POS.Domain.Interfaces;

namespace POS.Infrastructure.Hardware;

public class BarcodeScannerService : IBarcodeScannerService, IDisposable
{
    public event EventHandler<string>? BarcodeReceived;
    public BarcodeScannerConfig Config { get; private set; } = new();
    public bool IsRunning { get; private set; }

    private SerialPort? _serialPort;
    private string _buffer = string.Empty;
    private readonly object _lock = new();

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        if (Config.Mode == BarcodeScannerMode.Serial)
        {
            _serialPort = new SerialPort(Config.PortName, Config.BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            try
            {
                _serialPort.Open();
                _serialPort.DataReceived += OnDataReceived;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to open serial port {Config.PortName}: {ex.Message}");
                _serialPort?.Dispose();
                _serialPort = null;
                return Task.CompletedTask;
            }
        }

        IsRunning = true;
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (!IsRunning) return Task.CompletedTask;

        if (_serialPort is not null)
        {
            try
            {
                _serialPort.DataReceived -= OnDataReceived;
                if (_serialPort.IsOpen)
                    _serialPort.Close();
            }
            catch { }
            _serialPort.Dispose();
            _serialPort = null;
        }

        IsRunning = false;
        return Task.CompletedTask;
    }

    public void UpdateConfig(BarcodeScannerConfig config)
    {
        var wasRunning = IsRunning;
        if (wasRunning)
            StopAsync().GetAwaiter().GetResult();

        Config = config;

        if (wasRunning)
            StartAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    public void SimulateBarcode(string barcode)
    {
        BarcodeReceived?.Invoke(this, barcode);
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort is null || !_serialPort.IsOpen) return;

        try
        {
            var data = _serialPort.ReadExisting();
            string accumulated;

            lock (_lock)
            {
                accumulated = _buffer + data;
            }

            var lines = accumulated.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
            var complete = lines.Length > 1 ? lines[..^1] : Array.Empty<string>();
            var remaining = lines.Length > 0 ? lines[^1] : string.Empty;

            lock (_lock)
            {
                _buffer = remaining;
            }

            foreach (var barcode in complete)
            {
                var trimmed = barcode.Trim();
                if (trimmed.Length > 0)
                    BarcodeReceived?.Invoke(this, trimmed);
            }
        }
        catch
        {
        }
    }
}
