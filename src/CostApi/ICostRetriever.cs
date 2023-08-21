using AzurePriceCli.Commands;

namespace AzurePriceCli.CostApi;

public interface ICostRetriever
{

  public Task<CostResourceItem> RetrieveCostForResource(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId,
        MetricType metric,
        bool excludeMeterDetails,
        TimeframeType timeFrame,
        DateOnly from,
        DateOnly to
    );

  public Task<IEnumerable<CostResourceItem>> RetrieveCostForResources(
      bool includeDebugOutput,
      Guid subscriptionId,
      string[] filter,
      MetricType metric,
      bool excludeMeterDetails,
      TimeframeType timeFrame,
      DateOnly from,
      DateOnly to);
}