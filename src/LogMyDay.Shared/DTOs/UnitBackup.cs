namespace LogMyDay.Shared.DTOs;

public class UnitBackup
{
    public string Key { get; set; } = string.Empty; // Unit identifier (Name)
    public string Symbol { get; set; } = string.Empty;
    public double AToBase { get; set; }
    public double BToBase { get; set; }
    public int Decimals { get; set; }
    public string? QuantityKey { get; set; } // Reference to Quantity
}
