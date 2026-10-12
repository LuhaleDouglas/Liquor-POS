namespace LiquorPOS.Api.Models;

public class PriceGroup
{
    public int PriceGroupId { get; set; }

    public int BusinessId { get; set; }

    public string PriceGroupCode { get; set; } = string.Empty;

    public string PriceGroupName { get; set; } = string.Empty;
}