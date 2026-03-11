namespace LogMyDay.Shared.Scanning;

public interface IScanOrchestrator
{
    Task<ScanResult> Process(string scannedValue);
}
