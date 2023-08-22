using AzurePriceCli.Commands;

namespace AzurePriceCli.CostApi;

public interface ICostRetriever
{

  public Task<IEnumerable<ResourceCostItem>> RetrieveCostForResourceAsync(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId,
        MetricType metric,
        TimeframeType timeFrame
    );

  public Task<double> RetrieveForecastedCostsAsync(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId,
        MetricType metric,
        TimeframeType timeFrame
    );
}