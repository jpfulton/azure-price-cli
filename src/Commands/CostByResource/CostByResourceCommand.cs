
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AzurePriceCli.CostApi;
using AzurePriceCli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace AzurePriceCli.Commands.CostByResource;

public class ResourceAndCosts
{
    public Resource Resource { get; set; }
    public double CurrentCost { get; set;}
    public double ForecastCost { get; set; }

    public ResourceAndCosts(
        Resource resource,
        double currentCost,
        double forecastCost
    )
    {
        Resource = resource;
        CurrentCost = currentCost;
        ForecastCost = forecastCost;
    }
}

public class CostByResourceCommand : AsyncCommand<CostByResourceSettings>
{
    private readonly ICostRetriever _costRetriever;

    public CostByResourceCommand(ICostRetriever costRetriever)
    {
      _costRetriever = costRetriever;
    }

    public override ValidationResult Validate(CommandContext context, CostByResourceSettings settings)
    {
        if (string.IsNullOrEmpty(settings.ResourceGroup))
        {
            return ValidationResult.Error("Resource group option must be supplied.");
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

        string[] resourceIds = new string[0];
        var resourceCosts = new Dictionary<string, ResourceAndCosts>();

        var timer = new Stopwatch();

        timer.Start();
        await AnsiConsole.Status()
            .StartAsync("Fetching resource ids for group...", async ctx =>
            {
                resourceIds = await AzCommand.GetAzureResourceIdsAsync(settings.ResourceGroup);
            });
        timer.Stop();
        AnsiConsole.WriteLine($"Resource ids fetched in {timer.Elapsed.TotalSeconds}s.");

        timer.Restart();
        await AnsiConsole.Status()
            .StartAsync("Fetching current cost data for resources...", async ctx =>
            {
                foreach (var resourceId in resourceIds)
                {
                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.WriteLine($"Getting cost data for {resourceId}");
                    }

                    var resourceCostItems = await _costRetriever.RetrieveCostForResourceAsync(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.Timeframe
                    );

                    // pull display values from first item
                    var item = resourceCostItems.First();
                    var resource = new Resource()
                    {
                        Id = resourceId,
                        ArmLocation = item.ResourceLocation,
                        Name = item.GetResourceName(),
                        ServiceName = item.ServiceName,
                        ServiceTier = item.ServiceTier,
                        ResourceType = item.ResourceType,
                    };

                    // sum cost items to account for multiple meters
                    var resourceCost = resourceCostItems.Sum(x => x.Cost);

                    var resourceAndCosts = new ResourceAndCosts(resource, resourceCost, 0.0);
                    resourceCosts.Add(resourceId, resourceAndCosts);

                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine($"Cost and resource data for: {resourceId}");
                        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(resourceAndCosts)));
                    }
                }
            });
        timer.Stop();
        AnsiConsole.WriteLine($"Resource cost data fetched in {timer.Elapsed.TotalSeconds}s.");

        timer.Restart();
        await AnsiConsole.Status()
            .StartAsync("Fetching forecasted cost data for resources...", async ctx =>
            {
                foreach (var resourceId in resourceIds)
                {
                    var forecastCost = await _costRetriever.RetrieveForecastedCostsAsync(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.Timeframe
                    );

                    resourceCosts[resourceId].ForecastCost = forecastCost;
                }
            });
        timer.Stop();
        AnsiConsole.WriteLine($"Resource forecasted cost fetched in {timer.Elapsed.TotalSeconds}s.");

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Service")
            .AddColumn("Tier")
            .AddColumn("Current")
            .AddColumn("Forecast");

        var totalCost = 0.0;
        var forecastCost = 0.0;
        foreach (var item in resourceCosts.Values)
        {
            table.AddRow(
                new Markup(item.Resource.Name.EscapeMarkup()),
                new Markup(item.Resource.ResourceType.EscapeMarkup()),
                new Markup(item.Resource.ServiceName.EscapeMarkup()),
                new Markup(item.Resource.ServiceTier.EscapeMarkup()),
                new Markup(FormatDouble(item.CurrentCost).EscapeMarkup()),
                new Markup(FormatDouble(item.CurrentCost + item.ForecastCost).EscapeMarkup())
            );

            totalCost += item.CurrentCost;
            forecastCost += item.ForecastCost;
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
            new Markup(FormatDouble(totalCost).EscapeMarkup()),
            new Markup(FormatDouble(totalCost + forecastCost).EscapeMarkup())
        );

        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Current Billing Period Cost and Forecast by Resource");
        AnsiConsole.WriteLine($"Subscription: {settings.Subscription}");
        AnsiConsole.WriteLine($"Resource group: {settings.ResourceGroup}");
        AnsiConsole.Write(table);

        return 0;
    }

    private static string FormatDouble(double value)
    {
        return string.Format("{0:0.00}", value);
    }
}