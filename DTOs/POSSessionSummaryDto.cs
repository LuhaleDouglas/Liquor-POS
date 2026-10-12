namespace LiquorPOS.Api.DTOs;

public class POSSessionSummaryDto
{
    public long POSSessionId { get; set; }

    public int POSTerminalId { get; set; }

    public int UserId { get; set; }

    public DateTime OpenedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? CashDifference { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }


    // ------------------------------------------------------------
    // SALES SUMMARY
    // ------------------------------------------------------------

    public int NumberOfSales { get; set; }

    public decimal TotalSales { get; set; }


    // ------------------------------------------------------------
    // PAYMENT SUMMARY
    // ------------------------------------------------------------

    public decimal CashSales { get; set; }

    public decimal MpesaSales { get; set; }

    public decimal OtherPayments { get; set; }
}