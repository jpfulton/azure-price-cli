using System.ComponentModel;
using Spectre.Console.Cli;

namespace AzurePriceCli.Commands.PriceByResource;

public class PriceByResourceSettings : Settings
{
    [CommandOption("-r|--resource-group")]
    [Description("Resource group to use.")]
    public string ResourceGroup { get; set; } 
}