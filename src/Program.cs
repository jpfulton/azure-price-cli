using AzurePriceCli.Commands.CostByResource;
using AzurePriceCli.Commands.PriceByResource;
using AzurePriceCli.CostApi;
using AzurePriceCli.Infrastructure;
using AzurePriceCli.PriceApi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddHttpClient("CostApi", client =>
{
  client.BaseAddress = new Uri("https://management.azure.com/");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());

services.AddHttpClient("PriceApi", client =>
{
  client.BaseAddress = new Uri("https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());

services.AddTransient<ICostRetriever, AzureCostRetriever>();
services.AddTransient<IPriceRetriever, AzurePriceRetriever>();

var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
  config.SetApplicationName("azure-price");

#if DEBUG
  config.PropagateExceptions();
#endif

  config.AddCommand<CostByResourceCommand>("costByResource")
    .WithDescription("Show cost by resource within a resource group.");

  config.AddCommand<PriceByResourceCommand>("priceByResource")
    .WithDescription("Show price by resource within a resource group.");

  config.ValidateExamples();
});

return await app.RunAsync(args);