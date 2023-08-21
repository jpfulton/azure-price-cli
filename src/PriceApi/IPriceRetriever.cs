
namespace AzurePriceCli.PriceApi;

public interface IPriceRetriever
{
  Task<PriceItem> GetPriceItemAsync(string type, string skuName, string location);
}