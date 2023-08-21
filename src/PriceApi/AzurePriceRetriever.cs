
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

    public async Task<PriceItem> GetPriceItemAsync(string type, string skuName, string location)
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var sb = new StringBuilder();
        sb.Append("$filter=");
        sb.Append($"armRegionName eq '{location}'");
        sb.Append("and contains(serviceFamily, 'Network')");
        sb.Append("and contains(serviceName, 'dnszones')");

        var filter = HttpUtility.UrlEncode(sb.ToString());

        var uri = new Uri($"?{sb}", UriKind.Relative);
        var response = await _client.GetAsync(uri);

        response.EnsureSuccessStatusCode();

        var parsedResponse = await response.Content.ReadFromJsonAsync<PriceApiResponse>();

        var formattedOutput = JsonSerializer.Serialize<PriceApiResponse>(parsedResponse, new JsonSerializerOptions { WriteIndented = true });
        AnsiConsole.WriteLine(formattedOutput);

        return null;
    }
}