namespace AzurePriceCli.PriceApi;

public class PriceApiResponse 
{
  public string BillingCurrency { get; set; }
  public string CustomerEntityId { get; set; }
  public string CustomerEntityType { get; set; }
  public PriceItem[] Items { get; set; }
}