namespace AzurePriceCli.PriceApi;

public record PriceItem(
  string CurrencyCode,
  double TierMinimumUnits,
  string ReservationTerm,
  double RetailPrice,
  double UnitPrice,
  string ArmRegionName,
  string Location,
  DateTime EffectiveStartDate,
  Guid MeterId,
  string MeterName,
  string ProductId,
  string SkuId,
  string ProductName,
  string SkuName,
  string ServiceName,
  string ServiceId,
  string ServiceFamily,
  string UnitOfMeasure,
  string Type,
  bool IsPrimaryMeterRegion,
  string ArmSkuName
);