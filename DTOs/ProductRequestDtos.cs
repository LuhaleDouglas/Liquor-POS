namespace LiquorPOS.Api.DTOs;

public class CreateProductRequest
{
public string ProductCode { get; set; } = string.Empty;


public string ProductName { get; set; } = string.Empty;

public string? Barcode { get; set; }

public string? Brand { get; set; }

public string? Description { get; set; }

// 0 = No Category assigned yet
public int CategoryId { get; set; } = 0;

public List<CreateProductUnitRequest> Units { get; set; } = new();


}

public class CreateProductUnitRequest
{
public int UnitId { get; set; }


public decimal ConversionFactor { get; set; }

public string? Barcode { get; set; }

public bool IsBaseUnit { get; set; }

public decimal? RetailPrice { get; set; }

public decimal? WholesalePrice { get; set; }


}

public class UpdateProductRequest
{
public string ProductCode { get; set; } = string.Empty;

public string ProductName { get; set; } = string.Empty;

public string? Barcode { get; set; }

public string? Brand { get; set; }

public string? Description { get; set; }

// 0 = No Category assigned yet
public int CategoryId { get; set; } = 0;

public List<UpdateProductUnitRequest> Units { get; set; } = new();


}

public class UpdateProductUnitRequest
{
public int ProductUnitId { get; set; }


public int UnitId { get; set; }

public decimal ConversionFactor { get; set; }

public string? Barcode { get; set; }

public bool IsBaseUnit { get; set; }

public decimal? RetailPrice { get; set; }

public decimal? WholesalePrice { get; set; }
}