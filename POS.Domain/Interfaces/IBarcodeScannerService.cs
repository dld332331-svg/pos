namespace POS.Domain.Interfaces;

public enum BarcodeScannerMode
{
    KeyboardWedge = 0,
    Serial
}

public class BarcodeScannerConfig
{
    public BarcodeScannerMode Mode { get; set; } = BarcodeScannerMode.KeyboardWedge;
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
}

public interface IBarcodeScannerService
{
    event EventHandler<string>? BarcodeReceived;
    BarcodeScannerConfig Config { get; }
    bool IsRunning { get; }
    Task StartAsync();
    Task StopAsync();
    void UpdateConfig(BarcodeScannerConfig config);
}
