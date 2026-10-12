namespace LiquorPOS.Api.Models;

public class Supplier
{
    public int SupplierId { get; set; }

    public int BusinessId { get; set; }

    public string SupplierCode { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? KRA_PIN { get; set; }

    public string? Address { get; set; }

    public bool IsActive { get; set; }


    // Navigation properties

    public ICollection<Purchase> Purchases { get; set; }
        = new List<Purchase>();
}
