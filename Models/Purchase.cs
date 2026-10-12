
namespace LiquorPOS.Api.Models;

public class Purchase
{
    public long PurchaseId { get; set; }

    public int BusinessId { get; set; }

    public int BranchId { get; set; }

    public int SupplierId { get; set; }

    public int CreatedBy { get; set; }

    public string PurchaseNumber { get; set; } = string.Empty;

    public DateTime PurchaseDate { get; set; }

    public decimal SubTotal { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }


    // Navigation properties

    public Supplier? Supplier { get; set; }

    public ICollection<PurchaseLine> PurchaseLines { get; set; }
        = new List<PurchaseLine>();
}
