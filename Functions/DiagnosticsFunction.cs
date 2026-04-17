using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BirthRegistry.Functions;

/// <summary>
/// GET /api/diagnostics  — tests Datadog log intake connectivity from inside Azure.
/// Protected with Function key so it's not publicly accessible.
/// </summary>
public class DiagnosticsFunction(IConfiguration config)
{
    private static readonly HttpClient Http = new();

    [Function("Diagnostics")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "diagnostics")] HttpRequest req)
    {
        var apiKey  = config["DD_API_KEY"]  ?? "(not set)";
        var site    = config["DD_SITE"]     ?? "(not set)";
        var service = config["DD_SERVICE"]  ?? "(not set)";
        var env     = config["DD_ENV"]      ?? "(not set)";
        var profilerPath = config["CORECLR_PROFILER_PATH"] ?? "(not set)";

        // Test 1: Does the profiler .so file exist on disk?
        bool profilerExists = File.Exists(profilerPath);

        // Test 2: Send a log directly to Datadog HTTP intake via HttpClient
        var intakeUrl  = $"https://http-intake.logs.datadoghq.com/api/v2/logs";
        string intakeStatus = "untested";
        string intakeBody   = "";

        try
        {
            var payload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    ddsource = "csharp",
                    service,
                    env,
                    message  = $"[Diagnostics] Connectivity test from birthregistry-func at {DateTime.UtcNow:O}",
                    hostname = Environment.MachineName
                }
            });

            using var postReq = new HttpRequestMessage(HttpMethod.Post, intakeUrl);
            postReq.Headers.Add("DD-API-KEY", apiKey);
            postReq.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(postReq);
            intakeStatus = ((int)response.StatusCode).ToString();
            intakeBody   = await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            intakeStatus = "Exception";
            intakeBody   = ex.Message;
        }

        return new OkObjectResult(new
        {
            timestamp = DateTime.UtcNow,
            config = new
            {
                DD_API_KEY  = apiKey.Length > 6 ? $"{apiKey[..6]}...{apiKey[^4..]}" : "(too short)",
                DD_SITE     = site,
                DD_SERVICE  = service,
                DD_ENV      = env,
                CORECLR_PROFILER_PATH = profilerPath,
                profilerFileExists    = profilerExists
            },
            datadogLogsIntake = new
            {
                url    = intakeUrl,
                status = intakeStatus,
                body   = intakeBody
            }
        });
    }
}
