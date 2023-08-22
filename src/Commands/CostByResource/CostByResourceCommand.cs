
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using AzurePriceCli.CostApi;
using AzurePriceCli.Infrastructure;
using AzurePriceCli.PriceApi;
using Microsoft.Identity.Client;
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
    private readonly IPriceRetriever _priceRetriever;

    public CostByResourceCommand(
        ICostRetriever costRetriever,
        IPriceRetriever priceRetriever
    )
    {
        _costRetriever = costRetriever;
        _priceRetriever = priceRetriever;
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
        var retailPrices = new List<PriceItem>();

        await AnsiConsole.Progress()
            .AutoRefresh(true) // Turn off auto refresh
            .AutoClear(false)   // Do not remove the task list when done
            .HideCompleted(false)   // Hide tasks as they are completed
            .Columns(new ProgressColumn[] 
            {
                new SpinnerColumn(),            // Spinner
                new TaskDescriptionColumn(),    // Task description
                new ProgressBarColumn(),        // Progress bar
                new PercentageColumn(),         // Percentage
                new ElapsedTimeColumn(),        // Elapsed time
            })
            .StartAsync(async ctx =>
            {
                var resourceIdsTask = ctx.AddTask("[green]Getting resource ids[/]", new ProgressTaskSettings { AutoStart = false });
                var costTask = ctx.AddTask("[green]Getting current cost data[/]", new ProgressTaskSettings { AutoStart = false });
                var forecastTask = ctx.AddTask("[green]Getting forecasted cost data for resources[/]", new ProgressTaskSettings { AutoStart = false });
                var retailTask = ctx.AddTask("[green]Getting retail cost data for resources[/]", new ProgressTaskSettings { AutoStart = false });

                resourceIdsTask.StartTask();
                resourceIds = await AzCommand.GetAzureResourceIdsAsync(settings.ResourceGroup);
                resourceIdsTask.Increment(100.0);
                resourceIdsTask.StopTask();

                var resourceCount = resourceIds.Length;
                var resourceCounter = 0;
                var progressIncrement = 100.0 / resourceCount;

                costTask.StartTask();
                foreach (var resourceId in resourceIds)
                {
                    resourceCounter += 1;

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
                    var firstItem = resourceCostItems.First();
                    var resource = new Resource()
                    {
                        Id = resourceId,
                        Name = firstItem.GetResourceName(),
                        ResourceType = firstItem.ResourceType,
                    };
                    foreach (var item in resourceCostItems)
                    {
                        var meter = new Meter(
                            item.ResourceLocation,
                            item.ServiceName,
                            item.ServiceTier,
                            item.Meter,
                            item.Cost
                        );
                        resource.Meters.Add(meter);
                    }

                    // sum cost items to account for multiple meters
                    var resourceCost = resourceCostItems.Sum(x => x.Cost);

                    var resourceAndCosts = new ResourceAndCosts(resource, resourceCost, 0.0);
                    resourceCosts.Add(resourceId, resourceAndCosts);

                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine($"Cost and resource data for: {resourceId}");
                        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(resourceAndCosts)));
                    }

                    costTask.Increment(progressIncrement);
                }
                costTask.StopTask();
                
                resourceCounter = 0;
                progressIncrement = 100.0 / resourceCount;

                forecastTask.StartTask();
                foreach (var resourceId in resourceIds)
                {
                    resourceCounter += 1;

                    var forecastCost = await _costRetriever.RetrieveForecastedCostsAsync(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.Timeframe
                    );

                    resourceCosts[resourceId].ForecastCost = forecastCost;

                    forecastTask.Increment(progressIncrement);
                }
                forecastTask.StopTask();

                retailTask.StartTask();
                var allMeters = new List<Meter>();
                foreach (var resource in resourceCosts.Values)
                {
                    allMeters.AddRange(resource.Resource.Meters);
                }

                var distinctMeters = allMeters.GroupBy(x => new
                {
                    x.ArmLocation,
                    x.ServiceName,
                    x.ServiceTier,
                    x.MeterName
                })
                .Select(x => x.First());

                var metersCount = distinctMeters.Count();
                progressIncrement = 100.0 / metersCount;

                foreach (var meter in distinctMeters)
                {
                    metersCount += 1;

                    var priceItems = await _priceRetriever.GetPriceItemAsync(
                        false,
                        meter.ArmLocation,
                        meter.ServiceName,
                        meter.MeterName
                    );

                    retailPrices.AddRange(priceItems);

                    retailTask.Increment(progressIncrement);
                }
                retailTask.StopTask();
            });

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Service")
            .AddColumn("Tier")
            .AddColumn("Meter")
            .AddColumn("Retail")
            .AddColumn(new TableColumn("Current").RightAligned())
            .AddColumn(new TableColumn("Forecast").RightAligned());

        var totalCost = 0.0;
        var forecastCost = 0.0;
        foreach (var item in resourceCosts.Values)
        {
            table.AddRow(
                new Markup($"[bold]{item.Resource.Name}[/]"),
                new Markup($"[bold]{item.Resource.ResourceType}[/]"),
                new Markup("---".EscapeMarkup()),
                new Markup("---".EscapeMarkup()),
                new Markup("---".EscapeMarkup()),
                new Markup("---".EscapeMarkup()),
                new Markup($"[bold blue]{FormatDouble(item.CurrentCost)}[/]"),
                new Markup($"[bold blue]{FormatDouble(item.CurrentCost + item.ForecastCost)}[/]")
            );

            foreach (var meter in item.Resource.Meters)
            {
                table.AddRow(
                    new Markup(string.Empty.EscapeMarkup()),
                    new Markup(string.Empty.EscapeMarkup()),
                    new Markup(meter.ServiceName.EscapeMarkup()),
                    new Markup(meter.ServiceTier.EscapeMarkup()),
                    GetMeterTree(meter, retailPrices),
                    new Markup($"[italic dim]{GetRetailPrice(retailPrices, meter)}[/]"),
                    new Markup($"[italic dim]{FormatDouble(meter.Cost)}[/]"),
                    new Markup(string.Empty.EscapeMarkup())
                );
            }

            totalCost += item.CurrentCost;
            forecastCost += item.ForecastCost;
        }

        table.AddRow(
            new Markup("---".EscapeMarkup()),
            new Markup("---".EscapeMarkup()),
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
            new Markup(string.Empty.EscapeMarkup()),
            new Markup(string.Empty.EscapeMarkup()),
            new Markup($"[bold blue]{FormatDouble(totalCost)}[/]"),
            new Markup($"[bold blue]{FormatDouble(totalCost + forecastCost)}[/]")
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

    private static string GetRetailPrice(
        IEnumerable<PriceItem> priceItems,
        Meter meter
    )
    {
        var item = GetPriceItem(priceItems, meter);
        var value = item == null ? 0.0 : item.RetailPrice;

        return FormatDouble(value);
    }

    private static PriceItem? GetPriceItem(
        IEnumerable<PriceItem> priceItems,
        Meter meter
    )
    {
        var value = priceItems.Where(x =>
            (meter.ArmLocation.Equals("Unknown") || x.ArmRegionName.Equals(meter.ArmLocation)) &&
            x.ServiceName.Equals(meter.ServiceName) &&
            x.MeterName.Equals(meter.MeterName)
        )
        .FirstOrDefault();

        return value;
    }

    private static Tree GetMeterTree(Meter meter, IEnumerable<PriceItem> priceItems)
    {
        var item = GetPriceItem(priceItems, meter);
        var tree = new Tree(meter.MeterName);

        if (item != null)
        {
            tree.AddNode(new Markup($"[dim]Unit[/]: [italic dim]{item.UnitOfMeasure}[/]"));
            tree.AddNode(new Markup($"[dim]Price[/]: [italic dim]{FormatDouble(item.UnitPrice)}[/]"));
        }

        return tree;
    }
}