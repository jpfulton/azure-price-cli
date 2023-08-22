
using System.Runtime.InteropServices;
using System.Text.Json;
using AzurePriceCli.CostApi;
using AzurePriceCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace AzurePriceCli.Commands.CostByResource;

public class CostByResourceCommand : AsyncCommand<CostByResourceSettings>
{
    private readonly ICostRetriever _costRetriever;

    public CostByResourceCommand(ICostRetriever costRetriever)
    {
      _costRetriever = costRetriever;
    }

    public override ValidationResult Validate(CommandContext context, CostByResourceSettings settings)
    {
        // Validate if the timeframe is set to Custom, then the from and to dates must be specified and the from date must be before the to date
        if (settings.Timeframe == TimeframeType.Custom)
        {
            if (settings.From == null)
            {
                return ValidationResult.Error("The from date must be specified when the timeframe is set to Custom.");
            }

            if (settings.To == null)
            {
                return ValidationResult.Error("The to date must be specified when the timeframe is set to Custom.");
            }

            if (settings.From > settings.To)
            {
                return ValidationResult.Error("The from date must be before the to date.");
            }
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, CostByResourceSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(CostByResourceCommand).Assembly.GetName().Version}");


        // Get the subscription ID from the settings
        var subscriptionId = settings.Subscription;

        if (subscriptionId == Guid.Empty)
        {
            // Get the subscription ID from the Azure CLI
            try
            {
                if (settings.Debug)
                    AnsiConsole.WriteLine(
                        "No subscription ID specified. Trying to retrieve the default subscription ID from Azure CLI.");

                subscriptionId = Guid.Parse(await AzCommand.GetDefaultAzureSubscriptionIdAsync());

                if (settings.Debug)
                    AnsiConsole.WriteLine($"Default subscription ID retrieved from az cli: {subscriptionId}");

                settings.Subscription = subscriptionId;
            }
            catch (Exception e)
            {
                AnsiConsole.WriteException(new ArgumentException(
                    "Missing subscription ID. Please specify a subscription ID or login to Azure CLI.", e));
                return -1;
            }
        }

        var resourceIds = await AzCommand.GetAzureResourceIdsAsync(settings.ResourceGroup);
        var resourceCosts = new List<Tuple<CostResourceItem, CostItem>>();

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data for resources...", async ctx =>
            {
                foreach (var resourceId in resourceIds)
                {
                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.WriteLine($"Getting cost data for {resourceId}");
                    }

                    var resourceCost = await _costRetriever.RetrieveCostForResourceAsync(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.ExcludeMeterDetails,
                        settings.Timeframe,
                        settings.From,
                        settings.To
                    );

                    var forecastCost = await _costRetriever.RetrieveForecastedCostsAsync(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.Timeframe,
                        settings.From,
                        settings.To
                    );

                    resourceCosts.Add(new Tuple<CostResourceItem, CostItem>(resourceCost, forecastCost));

                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine($"Cost data for: {resourceId}");
                        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(resourceCost)));
                        AnsiConsole.WriteLine($"Forecast data for: {resourceId}");
                        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(forecastCost)));
                    }
                }
            });

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Service")
            .AddColumn("Tier")
            .AddColumn("Cost USD")
            .AddColumn("Forecast USD");

        var totalCost = 0.0;
        var forecastCost = 0.0;
        foreach (var cost in resourceCosts)
        {
            table.AddRow(
                new Markup(cost.Item1.ResourceId.Split("/").Last().EscapeMarkup()),
                new Markup(cost.Item1.ResourceType.EscapeMarkup()),
                new Markup(cost.Item1.ServiceName.EscapeMarkup()),
                new Markup(cost.Item1.ServiceTier.EscapeMarkup()),
                new Markup(Math.Round(cost.Item1.CostUSD, 2).ToString().EscapeMarkup()),
                new Markup(Math.Round(cost.Item2.CostUsd, 2).ToString().EscapeMarkup())
            );

            totalCost += cost.Item1.CostUSD;
            forecastCost += cost.Item2.CostUsd;
        }

        table.AddRow(
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup())
        );

        table.AddRow(
            new Markup("Total".EscapeMarkup()),
            new Markup(string.Empty.EscapeMarkup()),
            new Markup(string.Empty.EscapeMarkup()),
            new Markup(string.Empty.EscapeMarkup()),
            new Markup(Math.Round(totalCost, 2).ToString().EscapeMarkup()),
            new Markup(Math.Round(forecastCost, 2).ToString().EscapeMarkup())
        );

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine($"Subscription: {settings.Subscription}");
        AnsiConsole.WriteLine($"Resource group: {settings.ResourceGroup}");
        AnsiConsole.Write(table);

        return 0;
    }
}