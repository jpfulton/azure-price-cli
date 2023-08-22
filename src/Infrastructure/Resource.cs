namespace AzurePriceCli.Infrastructure;

public class Resource
{
  public string Id { get; set; }

  public string ResourceType { get; set; }

  public string Name { get; set; }

  public string PrimaryArmLocation { get; set; }
  public string ArmSkuName { get; set; }

  public List<Meter> Meters { get; set; } = new List<Meter>();
}

public record Meter(
  string ArmLocation,
  string ServiceName,
  string? ServiceTier,
  string? MeterName,
  double Cost
);