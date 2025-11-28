namespace LogMyDay.Shared.DTOs;

public class UnitRequest
{
    public required string Key { get; set; }

    public required string Symbol { get; set; }

    public int QuantityId { get; set; }

    public double AToBase { get; set; }

    public double BToBase { get; set; }

    public int Decimals { get; set; }
}
