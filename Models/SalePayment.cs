namespace LiquorPOS.Api.Models;

public class SalePayment
{
    public long SalePaymentId { get; set; }

    public long SaleId { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; }

    public string? Notes { get; set; }

    public decimal? AmountTendered { get; set; }

    public decimal? ChangeAmount { get; set; }
     // M-Pesa fields
    public string? MerchantRequestId { get; set; }

    public string? CheckoutRequestId { get; set; }

    public string? MpesaReceiptNumber { get; set; }

    public DateTime? MpesaTransactionDate { get; set; }

    public string? MpesaPhoneNumber { get; set; }

    public int? MpesaResultCode { get; set; }

    public string? MpesaResultDescription { get; set; }
}