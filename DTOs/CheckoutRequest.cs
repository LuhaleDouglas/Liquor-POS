namespace LiquorPOS.Api.DTOs;

public class CheckoutRequest
{
    public long POSSessionId { get; set; }

    public int? CustomerId { get; set; }

    public int PriceGroupId { get; set; }

    public List<CheckoutLineRequest> Lines { get; set; } = new();

    public List<PaymentRequest> Payments { get; set; } = new();

    public string? Notes { get; set; }
}