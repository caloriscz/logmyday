namespace LogMyDay.Shared.DTOs;

public class QuantityResponse
{
    public int Id { get; set; }

    public required string Key { get; set; }

    public int BaseUnitId { get; set; }

    public required string BaseUnitKey { get; set; }

    public required string BaseUnitSymbol { get; set; }
}
