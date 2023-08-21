namespace AzurePriceCli.Infrastructure;

public class Resource
{
  public string Id { get; set; }

  public string Type { get; set; }

  public string Location { get; set; }

  public string Name { get; set; }

  public string SkuName { get; set; }
}