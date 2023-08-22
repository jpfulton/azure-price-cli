
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Spectre.Console;

namespace AzurePriceCli.PriceApi;

public class AzurePriceRetriever : IPriceRetriever
{
    private readonly HttpClient _client;

    public AzurePriceRetriever(IHttpClientFactory httpClientFactory)
    {
      _client = httpClientFactory.CreateClient("PriceApi");
    }

    public async Task<IEnumerable<PriceItem>> GetPriceItemAsync(
        bool includeDebugOutput,
        string location, 
        string serviceName,
        string meterName
    )
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var sb = new StringBuilder();
        sb.Append("$filter=");
        // sb.Append($"serviceName eq '{serviceName}'");
        // sb.Append($"and meterName eq '{meterName}'");
        sb.Append($"contains(serviceName, '{serviceName}')");
        sb.Append($" and contains(meterName, '{meterName}')");
        if (!location.Equals("Unknown"))
        {
            sb.Append($" and armRegionName eq '{location}'");
        }

        var filterClause = sb.ToString();

        var uri = new Uri($"?{filterClause}", UriKind.Relative);
        var response = await _client.GetAsync(uri);

        response.EnsureSuccessStatusCode();

        var parsedResponse = await response.Content.ReadFromJsonAsync<PriceApiResponse>();

        if (includeDebugOutput)
        {
            var formattedOutput = JsonSerializer.Serialize<PriceApiResponse>(parsedResponse, new JsonSerializerOptions { WriteIndented = true });
            AnsiConsole.WriteLine(formattedOutput);
            AnsiConsole.WriteLine();
        }

        return parsedResponse.Items;
    }
}