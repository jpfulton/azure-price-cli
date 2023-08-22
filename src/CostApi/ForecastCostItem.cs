namespace AzurePriceCli.CostApi;

public record ForecastCostItem(DateOnly Date, double Cost, double CostUSD, string Currency);