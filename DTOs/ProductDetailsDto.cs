namespace LiquorPOS.Api.DTOs;

public class ProductDetailsDto
{
    public int ProductId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string? Brand { get; set; }

    // 0 = No Category assigned yet
    public int CategoryId { get; set; }

    public decimal Stock { get; set; }

    public List<ProductUnitDto> Units { get; set; } = new();
}

public class ProductUnitDto
{
    public int ProductUnitId { get; set; }

    public string UnitCode { get; set; } = string.Empty;

    public string UnitName { get; set; } = string.Empty;

    public decimal ConversionFactor { get; set; }

    public bool IsBaseUnit { get; set; }

    public decimal RetailPrice { get; set; }

    public decimal WholesalePrice { get; set; }
}