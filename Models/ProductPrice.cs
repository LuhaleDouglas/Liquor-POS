namespace LiquorPOS.Api.Models;

public class ProductPrice
{
    public long ProductPriceId { get; set; }

    public int ProductId { get; set; }

    public int ProductUnitId { get; set; }

    public int PriceGroupId { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool IsActive { get; set; }
}