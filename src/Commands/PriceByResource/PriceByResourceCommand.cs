

using AzurePriceCli.Infrastructure;
using AzurePriceCli.PriceApi;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AzurePriceCli.Commands.PriceByResource;

public class PriceByResourceCommand : AsyncCommand<PriceByResourceSettings>
{

    private readonly IPriceRetriever _priceRetriever;

    public PriceByResourceCommand(IPriceRetriever priceRetriever)
    {
        _priceRetriever = priceRetriever;
    }

    public override ValidationResult Validate(CommandContext context, PriceByResourceSettings settings)
    {
        if (settings.ResourceGroup == null)
        {
            return ValidationResult.Error("Resource group must be specified.");
        }

        return ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, PriceByResourceSettings settings)
    {
        // Show version
        if (settings.Debug)
            AnsiConsole.WriteLine($"Version: {typeof(PriceByResourceCommand).Assembly.GetName().Version}");

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

        string[] resourceIds = await AzCommand.GetAzureResourceIdsAsync(settings.ResourceGroup);
        if (settings.Debug)
        {
            AnsiConsole.WriteLine("Resource IDs:");
            foreach (var id in resourceIds)
            {
                AnsiConsole.WriteLine(id);
            }
        }

        var resources = new List<Resource>();
        foreach (var id in resourceIds)
        {
            var resource = await AzCommand.GetAzureResourceByIdAsync(id);
            resources.Add(resource);
        }

        var result = await _priceRetriever.GetPriceItemAsync("test", "test", "northcentralus");

        return 0;
    }
}