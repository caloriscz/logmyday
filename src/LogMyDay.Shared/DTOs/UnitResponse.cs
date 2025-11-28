namespace LogMyDay.Shared.DTOs;

public class UnitResponse
{
    public int Id { get; set; }

    public required string Key { get; set; }

    public required string Symbol { get; set; }

    public int QuantityId { get; set; }

    public required string QuantityKey { get; set; }

    public double AToBase { get; set; }

    public double BToBase { get; set; }

    public int Decimals { get; set; }

    public bool IsBaseUnit { get; set; }
}
