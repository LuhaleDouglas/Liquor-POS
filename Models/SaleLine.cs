namespace LiquorPOS.Api.Models;

public class SaleLine
{
    public long SaleLineId { get; set; }

    public long SaleId { get; set; }

    public int ProductId { get; set; }

    public int ProductUnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }
}