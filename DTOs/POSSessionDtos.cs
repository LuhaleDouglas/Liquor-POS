namespace LiquorPOS.Api.DTOs;

public class OpenPOSSessionRequest
{
    public int POSTerminalId { get; set; }
    public decimal OpeningCash { get; set; }
    public string? Notes { get; set; }
}


public class ClosePOSSessionRequest
{
    public decimal ActualCash { get; set; }
    public string? Notes { get; set; }
}