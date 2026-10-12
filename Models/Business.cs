namespace LiquorPOS.Api.Models;

public class Business
{
    public int BusinessId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string? KRA_PIN { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }
    public string? PayBill { get; set; }

    public string? DevelopedBy { get; set; } 

    public DateTime CreatedAt { get; set; }

    public bool IsActive { get; set; }
}