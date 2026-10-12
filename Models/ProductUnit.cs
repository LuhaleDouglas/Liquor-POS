namespace LiquorPOS.Api.Models;

public class ProductUnit
{
    public int ProductUnitId { get; set; }

    public int ProductId { get; set; }

    public int UnitId { get; set; }

    public decimal ConversionFactor { get; set; }

    public string? Barcode { get; set; }

    public bool IsBaseUnit { get; set; }
}