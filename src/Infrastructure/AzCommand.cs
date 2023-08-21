using System.Diagnostics;
using System.Text.Json;
using Microsoft.VisualBasic;

namespace AzurePriceCli.Infrastructure;

public static class AzCommand
{
    public static string GetDefaultAzureSubscriptionId()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = "account show",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Error executing 'az account show': {error}");
            }

            using (var jsonDocument = JsonDocument.Parse(output))
            {
                JsonElement root = jsonDocument.RootElement;
                if (root.TryGetProperty("id", out JsonElement idElement))
                {
                    string subscriptionId = idElement.GetString();
                    return subscriptionId;
                }
                else
                {
                    throw new Exception("Unable to find the 'id' property in the JSON output.");
                }
            }
        }
    }

    public static string[] GetAzureResourceIds(string resourceGroup)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "az",
            Arguments = $"resource list --resource-group {resourceGroup}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string error = process.StandardError.ReadToEnd();
                throw new Exception($"Error executing 'az resource list': {error}");
            }

            var idList = new List<string>();

            using (var jsonDocument = JsonDocument.Parse(output))
            {
                JsonElement root = jsonDocument.RootElement;
                var arrayEnumerator = root.EnumerateArray();

                foreach (var element in arrayEnumerator)
                {
                    if (element.TryGetProperty("id", out JsonElement idElement))
                    {
                        string resourceId = idElement.GetString();
                        idList.Add(resourceId);
                    }
                    else
                    {
                        throw new Exception("Unable to find the 'id' property in the JSON output.");
                    }
                }
            }

            return idList.ToArray();
        }
    }
}