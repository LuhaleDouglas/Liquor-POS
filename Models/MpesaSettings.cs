namespace LiquorPOS.Api.Models;

public class MpesaSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ConsumerKey { get; set; } = string.Empty;

    public string ConsumerSecret { get; set; } = string.Empty;

    public string ShortCode { get; set; } = string.Empty;

    public string PassKey { get; set; } = string.Empty;

    public string CallbackUrl { get; set; } = string.Empty;

    public string TransactionType { get; set; } = string.Empty;

    public string AccountReference { get; set; } = string.Empty;

    public string TransactionDesc { get; set; } = string.Empty;
}