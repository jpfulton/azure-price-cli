namespace AzurePriceCli.CostApi;

public record CostItem(DateOnly Date, double Cost, double CostUsd, string Currency);