
namespace AzurePriceCli.PriceApi;

public interface IPriceRetriever
{
  public Task<IEnumerable<PriceItem>> GetPriceItemAsync(
      bool includeDebugOutput,
      string location,
      string serviceName,
      string meterName
  );
}