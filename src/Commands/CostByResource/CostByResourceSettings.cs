using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzurePriceCli.Commands.CostByResource;

public class CostByResourceSettings : Settings
{
    [CommandOption("-i|--resource-id")]
    [Description("Resource group to use.")]
    public string ResourceId { get; set; }

    [CommandOption("-t|--timeframe")]
    [Description(  "The timeframe to use for the costs. Defaults to BillingMonthToDate. When set to Custom, specify the from and to dates using the --from and --to options")]
    public TimeframeType Timeframe { get; set; } = TimeframeType.BillingMonthToDate;
    
    [CommandOption("--from")]
    [Description("The start date to use for the costs. Defaults to the first day of the previous month.")]
    public DateOnly From { get; set; } = DateOnly.FromDateTime( new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1));
    
    [CommandOption("--to")]
    [Description("The end date to use for the costs. Defaults to the current date.")]
    public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [CommandOption("-m|--metric")]
    [Description("The metric to use for the costs. Defaults to ActualCost. (ActualCost, AmortizedCost)")]
    [DefaultValue(MetricType.ActualCost)]
    public MetricType Metric { get; set; } = MetricType.ActualCost;

    [CommandOption("--exclude-meter-details")]
    [Description("Exclude meter details from the output.")]
    [DefaultValue(false)]
    public bool ExcludeMeterDetails { get; set; }
}