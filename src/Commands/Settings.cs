using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzurePriceCli.Commands;

public class Settings : CommandSettings
{
    [CommandOption("--debug")]
    [Description("Increase logging verbosity to show all debug logs.")]
    [DefaultValue(false)]
    public bool Debug { get; set; }

    [CommandOption("-s|--subscription")]
    [Description("The subscription id to use. Will try to fetch the active id if not specified.")]
    public Guid Subscription { get; set; }

    [CommandOption("-r|--resource-group")]
    [Description("Resource group to use.")]
    public string ResourceGroup { get; set; }
}

public enum MetricType
{
    ActualCost,
    AmortizedCost
}

public enum TimeframeType
{
    BillingMonthToDate,	
    MonthToDate,	
    TheLastBillingMonth,	
    TheLastMonth,
    WeekToDate,
}