namespace LiquorPOS.Api.Models;

public class POSSession
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
}