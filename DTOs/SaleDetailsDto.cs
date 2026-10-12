namespace LiquorPOS.Api.DTOs;

public class SaleDetailsDto
{
    public long SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }

    public int BusinessId { get; set; }
    public int BranchId { get; set; }
    public long POSSessionId { get; set; }
    public int UserId { get; set; }

    public int PriceGroupId { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public List<SaleLineDto> Lines { get; set; } = new();
    public List<SalePaymentDto> Payments { get; set; } = new();
}

public class SaleLineDto
{
    public long SaleLineId { get; set; }

    public int ProductId { get; set; }
    public int ProductUnitId { get; set; }

    public string ProductName { get; set; } = string.Empty;
    public string? ProductCode { get; set; }

    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalePaymentDto
{
    public long SalePaymentId { get; set; }

    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public string? ReferenceNumber { get; set; }

    public string PaymentStatus { get; set; } = string.Empty;

    public DateTime TransactionDate { get; set; }

    public decimal? AmountTendered { get; set; }
    public decimal? ChangeAmount { get; set; }

    public string? Notes { get; set; }
}