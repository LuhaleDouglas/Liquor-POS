namespace LiquorPOS.Api.DTOs;

public class StockAdjustmentRequest
{
    public int ProductId { get; set; }

    // Positive = increase
    // Negative = decrease
    public decimal Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string? Notes { get; set; }
}