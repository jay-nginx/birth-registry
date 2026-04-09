using BirthRegistry.Models;
using BirthRegistry.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace BirthRegistry.Functions;

/// <summary>
/// GET /api/search                 → search form
/// GET /api/search?lastName=Smith  → search results
/// GET /api/records/{id}           → view a single record
/// </summary>
public class SearchFunction(IBirthRegistryService service, ILogger<SearchFunction> logger)
{
    [Function("SearchForm")]
    public async Task<IActionResult> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "search")] HttpRequest req)
    {
        var lastName = req.Query["lastName"].ToString();
        var fromStr  = req.Query["from"].ToString();
        var toStr    = req.Query["to"].ToString();
        var page     = int.TryParse(req.Query["page"], out var p) && p > 0 ? p : 1;

        DateTime? from = DateTime.TryParse(fromStr, out var fd) ? fd : null;
        DateTime? to   = DateTime.TryParse(toStr,   out var td) ? td : null;

        bool isSearch = !string.IsNullOrWhiteSpace(lastName) || from.HasValue || to.HasValue;

        if (!isSearch)
        {
            return new ContentResult
            {
                Content = HtmlTemplates.SearchForm(null, null, null, null, 0),
                ContentType = "text/html",
                StatusCode = 200
            };
        }

        logger.LogInformation("Searching records. lastName={LastName} from={From} to={To} page={Page}",
            lastName, from, to, page);

        var (records, total) = await service.SearchAsync(lastName, from, to, page, 20);

        return new ContentResult
        {
            Content = HtmlTemplates.SearchForm(lastName, fromStr, toStr, records, total, page),
            ContentType = "text/html",
            StatusCode = 200
        };
    }

    [Function("GetRecord")]
    public async Task<IActionResult> GetRecord(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "records/{id}")] HttpRequest req,
        string id)
    {
        logger.LogInformation("Getting record {Id}", id);

        // Support lookup by registration number (BR-YYYY-XXXXXX) or GUID
        BirthRecord? record = null;
        if (id.StartsWith("BR-", StringComparison.OrdinalIgnoreCase))
        {
            record = await service.GetByRegistrationNumberAsync(id);
        }
        else if (Guid.TryParse(id, out var guid))
        {
            record = await service.GetByIdAsync(guid);
        }

        if (record is null)
        {
            return new ContentResult
            {
                Content = HtmlTemplates.ErrorPage($"Record '{id}' not found."),
                ContentType = "text/html",
                StatusCode = 404
            };
        }

        return new ContentResult
        {
            Content = HtmlTemplates.RecordDetail(record),
            ContentType = "text/html",
            StatusCode = 200
        };
    }
}
