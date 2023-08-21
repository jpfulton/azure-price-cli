

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Core;
using Azure.Identity;
using AzurePriceCli.Commands;
using Spectre.Console;
using Spectre.Console.Json;

namespace AzurePriceCli.CostApi;

public class AzureCostRetriever : ICostRetriever
{
    private readonly HttpClient _client;
    private bool _tokenRetrieved;

    public enum DimensionNames
    {
        PublisherType,
        ResourceGroupName,
        ResourceLocation,
        ResourceId,
        ServiceName,
        ServiceTier,
        ServiceFamily,
        InvoiceId,
        CustomerName,
        PartnerName,
        ResourceType,
        ChargeType,
        BillingPeriod,
        MeterCategory,
        MeterSubCategory,
        // Add more dimension names as needed
    }

    public AzureCostRetriever(IHttpClientFactory httpClientFactory)
    {
        _client = httpClientFactory.CreateClient("CostApi");
    }

    private async Task RetrieveToken(bool includeDebugOutput)
    {
        if (_tokenRetrieved)
            return;

        // Get the token by using the DefaultAzureCredential
        var tokenCredential = new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential());

        if (includeDebugOutput)
            AnsiConsole.WriteLine($"Using token credential: {tokenCredential.GetType().Name} to fetch a token.");

        var token = await tokenCredential.GetTokenAsync(new TokenRequestContext(new[]
            { $"https://management.azure.com/.default" }));

        if (includeDebugOutput)
            AnsiConsole.WriteLine($"Token retrieved and expires at: {token.ExpiresOn}");

        // Set as the bearer token for the HTTP client
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        _tokenRetrieved = true;
    }

    private object? GenerateFilters(string[]? filterArgs)
    {
        if (filterArgs == null || filterArgs.Length == 0)
            return null;

        var filters = new List<object>();
        foreach (var arg in filterArgs)
        {
            var filterParts = arg.Split('=');
            var name = filterParts[0];
            var values = filterParts[1].Split(';');

            // Define default filter dictionary
            var filterDict = new Dictionary<string, object>()
            {
                { "Name", name },
                { "Operator", "In" },
                { "Values", new List<string>(values) }
            };

            // Decide if this is a Dimension or a Tag filter
            if (Enum.IsDefined(typeof(DimensionNames), name))
            {
                filters.Add(new { Dimensions = filterDict });
            }
            else
            {
                filters.Add(new { Tags = filterDict });
            }
        }

        if (filters.Count > 1)
            return new
            {
                And = filters
            };
        else
            return filters[0];
    }

    private async Task<HttpResponseMessage> ExecuteCallToCostApi(bool includeDebugOutput, object? payload, Uri uri)
    {
        await RetrieveToken(includeDebugOutput);

        if (includeDebugOutput)
        {
            AnsiConsole.WriteLine($"Retrieving data from {uri} using the following payload:");
            AnsiConsole.Write(new JsonText(JsonSerializer.Serialize(payload)));
            AnsiConsole.WriteLine();
        }

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var response = payload == null
            ? await _client.GetAsync(uri)
            : await _client.PostAsJsonAsync(uri, payload, options);

        if (includeDebugOutput)
        {
            AnsiConsole.WriteLine(
                $"Response status code is {response.StatusCode} and got payload size of {response.Content.Headers.ContentLength}");
            if (!response.IsSuccessStatusCode)
            {
                AnsiConsole.WriteLine($"Response content: {await response.Content.ReadAsStringAsync()}");
            }
        }

        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<CostResourceItem> RetrieveCostForResource(
        bool includeDebugOutput,
        Guid subscriptionId,
        string resourceId, 
        MetricType metric, 
        bool excludeMeterDetails, 
        TimeframeType timeFrame, 
        DateOnly from,
        DateOnly to
    )
    {
        string[] filter = new string[] {$"ResourceId={resourceId}"};
        var costItems = await RetrieveCostForResources(
            includeDebugOutput,
            subscriptionId,
            filter,
            metric,
            excludeMeterDetails,
            timeFrame,
            from,
            to
        );

        return costItems.ToArray()[0];
    }

    public async Task<IEnumerable<CostResourceItem>> RetrieveCostForResources(
        bool includeDebugOutput,
        Guid subscriptionId, 
        string[] filter, 
        MetricType metric, 
        bool excludeMeterDetails, 
        TimeframeType timeFrame, 
        DateOnly from,
        DateOnly to)
    {
        var uri = new Uri(
            $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2021-10-01&$top=5000",
            UriKind.Relative);

        object grouping;
        if (excludeMeterDetails==false)
            grouping = new[]
            {
                new
                {
                    type = "Dimension",
                    name = "ResourceId"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceLocation"
                },
                new
                {
                    type = "Dimension",
                    name = "ChargeType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceGroupName"
                },
                new
                {
                    type = "Dimension",
                    name = "PublisherType"
                },
                new
                {
                    type = "Dimension",
                    name = "MeterCategory"
                },
                new
                {
                    type = "Dimension",
                    name = "MeterSubcategory"
                },
                new
                {
                    type = "Dimension",
                    name = "Meter"
                }
            };
        else
        {
            grouping = new[]
            {
                new
                {
                    type = "Dimension",
                    name = "ResourceId"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceLocation"
                },
                new
                {
                    type = "Dimension",
                    name = "ChargeType"
                },
                new
                {
                    type = "Dimension",
                    name = "ResourceGroupName"
                },
                new
                {
                    type = "Dimension",
                    name = "PublisherType"
                }
            };
        }
        
        var payload = new
        {
            type = metric.ToString(),
            timeframe = timeFrame.ToString(),
            timePeriod = timeFrame == TimeframeType.Custom
                ? new
                {
                    from = from.ToString("yyyy-MM-dd"),
                    to = to.ToString("yyyy-MM-dd")
                }
                : null,
            dataSet = new
            {
                granularity = "None",
                aggregation = new
                {
                    totalCost = new
                    {
                        name = "Cost",
                        function = "Sum"
                    },
                    totalCostUSD = new
                    {
                        name = "CostUSD",
                        function = "Sum"
                    }
                },
                include = new[] { "Tags" },
                filter = GenerateFilters(filter),
                grouping = grouping,
            }
        };
        var response = await ExecuteCallToCostApi(includeDebugOutput, payload, uri);

        CostQueryResponse? content = await response.Content.ReadFromJsonAsync<CostQueryResponse>();

        var items = new List<CostResourceItem>();
        foreach (JsonElement row in content.properties.rows)
        {
            double cost = row[0].GetDouble();
            double costUSD = row[1].GetDouble();
            string resourceId = row[2].GetString();
            string resourceType = row[3].GetString();
            string resourceLocation = row[4].GetString();
            string chargeType = row[5].GetString();
            string resourceGroupName = row[6].GetString();
            string publisherType = row[7].GetString();
          
            string serviceName = excludeMeterDetails?null:row[8].GetString();
            string serviceTier = excludeMeterDetails?null:row[9].GetString();
            string meter = excludeMeterDetails?null:row[10].GetString();
      
            int tagsColumn = excludeMeterDetails?8:11;
            // Assuming row[tagsColumn] contains the tags array
            var tagsArray = row[tagsColumn].EnumerateArray().ToArray();

            Dictionary<string, string> tags = new Dictionary<string, string>();

            foreach (var tagString in tagsArray)
            {
                var parts = tagString.GetString().Split(':');
                if (parts.Length == 2) // Ensure the string is in the format "key:value"
                {
                    var key = parts[0].Trim('"'); // Remove quotes from the key
                    var value = parts[1].Trim('"'); // Remove quotes from the value
                    tags[key] = value;
                }
            }
            
            int currencyColumn = excludeMeterDetails?9:12;
            string currency = row[currencyColumn].GetString();

            CostResourceItem item = new CostResourceItem(
                cost, 
                costUSD, 
                resourceId, 
                resourceType, 
                resourceLocation,
                chargeType, 
                resourceGroupName, 
                publisherType, 
                serviceName, 
                serviceTier, 
                meter, 
                tags, 
                currency);

            items.Add(item);
        }

        if (excludeMeterDetails)
        {
            // As we do not care about the meter details, we still have the possibility of resources with the same, but having multiple locations like Intercontinental, Unknown and Unassigned
            // We need to aggregate these resources together and show the total cost for the resource, the resource locations need to be combined as well. So it can become West Europe, Intercontinental
            
            var aggregatedItems = new List<CostResourceItem>();
            var groupedItems = items.GroupBy(x => x.ResourceId);
            foreach (var groupedItem in groupedItems)
            {
                var aggregatedItem = new CostResourceItem(groupedItem.Sum(x => x.Cost), groupedItem.Sum(x => x.CostUSD), groupedItem.Key, groupedItem.First().ResourceType, string.Join(", ", groupedItem.Select(x => x.ResourceLocation)), groupedItem.First().ChargeType, groupedItem.First().ResourceGroupName, groupedItem.First().PublisherType, null, null, null, groupedItem.First().Tags, groupedItem.First().Currency);
                aggregatedItems.Add(aggregatedItem);
            }
            
            return aggregatedItems;
        }
        return items;
    }
}