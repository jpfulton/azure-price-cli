
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
        var resourceCosts = new List<CostResourceItem>();

        await AnsiConsole.Status()
            .StartAsync("Fetching cost data for resource...", async ctx =>
            {
                foreach (var resourceId in resourceIds)
                {
                    var resourceCost = await _costRetriever.RetrieveCostForResource(
                        settings.Debug,
                        subscriptionId,
                        resourceId,
                        settings.Metric,
                        settings.ExcludeMeterDetails,
                        settings.Timeframe,
                        settings.From,
                        settings.To);

                    resourceCosts.Add(resourceCost);

                    if (settings.Debug)
                    {
                        AnsiConsole.WriteLine($"Cost data for: {resourceId}");
                        AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(resourceCost)));
                    }
                }
            });

        var table = new Table()
            .RoundedBorder()
            .Expand()
            .AddColumn("Name")
            .AddColumn("Type")
            .AddColumn("Cost USD");

        foreach (var cost in resourceCosts)
        {
            table.AddRow(
                new Markup(cost.ResourceId.Split("/").Last().EscapeMarkup()),
                new Markup(cost.ResourceType.EscapeMarkup()),
                new Markup(cost.CostUSD.ToString().EscapeMarkup())
            );
        }

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        return 0;
    }
}