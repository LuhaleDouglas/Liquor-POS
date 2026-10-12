namespace LiquorPOS.Api.Models;

public class StockTransaction
{
    public long StockTransactionId { get; set; }

    public int BusinessId { get; set; }

    public int BranchId { get; set; }

    public int ProductId { get; set; }

    public string TransactionType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitCost { get; set; }

    public string? ReferenceType { get; set; }

    public long? ReferenceId { get; set; }

    public DateTime TransactionDate { get; set; }

    public int UserId { get; set; }

    public string? Notes { get; set; }
}