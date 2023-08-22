using AzurePriceCli.Commands;

namespace AzurePriceCli.CostApi;

public interface ICostRetriever
{

  public Task<IEnumerable<ResourceCostItem>> RetrieveCostForResourceAsync(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId,
        MetricType metric,
        TimeframeType timeFrame,
        DateOnly from,
        DateOnly to
    );

  public Task<double> RetrieveForecastedCostsAsync(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId,
        MetricType metric,
        TimeframeType timeFrame,
        DateOnly from,
        DateOnly to
    );
}