namespace LiquorPOS.Api.DTOs;

public class CheckoutLineRequest
{
    public int ProductId { get; set; }

    public int ProductUnitId { get; set; }

    public decimal Quantity { get; set; }

    public decimal DiscountAmount { get; set; }
}