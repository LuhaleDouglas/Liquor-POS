namespace LiquorPOS.Api.Models;

public class Product
{
    public int ProductId { get; set; }

    public int BusinessId { get; set; }

    // 0 = No Category assigned
    public int CategoryId { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? Description { get; set; }
}