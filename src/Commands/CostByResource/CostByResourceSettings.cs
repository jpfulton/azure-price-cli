using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzurePriceCli.Commands.CostByResource;

public class CostByResourceSettings : Settings
{
    [CommandOption("-t|--timeframe")]
    [Description(  "The timeframe to use for the costs. Defaults to BillingMonthToDate.")]
    public TimeframeType Timeframe { get; set; } = TimeframeType.BillingMonthToDate;
    
    [CommandOption("-m|--metric")]
    [Description("The metric to use for the costs. Defaults to ActualCost. (ActualCost, AmortizedCost)")]
    [DefaultValue(MetricType.ActualCost)]
    public MetricType Metric { get; set; } = MetricType.ActualCost;
}