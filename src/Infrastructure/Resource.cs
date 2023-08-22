namespace AzurePriceCli.Infrastructure;

public class Resource
{
  public string Id { get; set; }

  public string ResourceType { get; set; }

  public string ArmLocation { get; set; }

  public string Name { get; set; }

  public string ArmSkuName { get; set; }

  public string ServiceName { get; set; }

  public string ServiceTier { get; set; }
}