namespace LiquorPOS.Api.DTOs;

public class PaymentRequest
{
    public string PaymentMethod { get; set; } = string.Empty;

    public decimal AmountTendered { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Notes { get; set; }
}