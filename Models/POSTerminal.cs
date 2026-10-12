namespace LiquorPOS.Api.Models;

public class POSTerminal
{
    public int POSTerminalId { get; set; }
    public int BusinessId { get; set; }
    public int BranchId { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
    public string TerminalName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}