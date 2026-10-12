namespace LiquorPOS.Api.Models;

public class PurchaseLine
{
    public long PurchaseLineId { get; set; }

    public long PurchaseId { get; set; }

    public int ProductId { get; set; }

    public int ProductUnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal LineTotal { get; set; }


    // Navigation properties

    public Purchase? Purchase { get; set; }

    public Product? Product { get; set; }

    public ProductUnit? ProductUnit { get; set; }
}
