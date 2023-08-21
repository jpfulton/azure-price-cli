﻿using AzurePriceCli.Commands.PriceByResource;
using AzurePriceCli.Infrastructure;
using AzurePriceCli.PriceApi;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();

services.AddHttpClient("PriceApi", client =>
{
  client.BaseAddress = new Uri("https://prices.azure.com/api/retail/prices?api-version=2023-01-01-preview");
  client.DefaultRequestHeaders.Add("Accept", "application/json");
}).AddPolicyHandler(PollyPolicyExtensions.GetRetryAfterPolicy());

services.AddTransient<IPriceRetriever, AzurePriceRetriever>();

var registrar = new TypeRegistrar(services);

var app = new CommandApp(registrar);

app.Configure(config =>
{
  config.SetApplicationName("azure-price");

  config.AddCommand<PriceByResourceCommand>("priceByResource")
    .WithDescription("Show price by resource within a resource group.");

  config.ValidateExamples();
});

return await app.RunAsync(args);