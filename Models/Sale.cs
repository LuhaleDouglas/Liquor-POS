namespace LiquorPOS.Api.Models;

public class Sale
{
    public long SaleId { get; set; }

    public int BusinessId { get; set; }

    public int BranchId { get; set; }

    public long POSSessionId { get; set; }

    public int UserId { get; set; }

    public int? CustomerId { get; set; }

    public string SaleNumber { get; set; } = string.Empty;

    public DateTime SaleDate { get; set; }

    public int PriceGroupId { get; set; }

    public decimal SubTotal { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
}