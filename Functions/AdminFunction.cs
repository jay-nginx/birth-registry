using BirthRegistry.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BirthRegistry.Functions;

/// <summary>
/// Admin / API endpoints (JSON) for programmatic access and status management.
/// PATCH /api/admin/records/{id}/status  → update status
/// DELETE /api/admin/records/{id}        → delete record
/// GET   /api/health                     → health check
/// </summary>
public class AdminFunction(IBirthRegistryService service, ILogger<AdminFunction> logger)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Function("HealthCheck")]
    public IActionResult HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequest req)
    {
        logger.LogInformation("Health check requested");
        return new OkObjectResult(new { status = "healthy", timestamp = DateTime.UtcNow, service = "birth-registry" });
    }

    [Function("UpdateStatus")]
    public async Task<IActionResult> UpdateStatus(
        [HttpTrigger(AuthorizationLevel.Function, "patch", Route = "admin/records/{id}/status")] HttpRequest req,
        string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return new BadRequestObjectResult(new { error = "Invalid record ID format." });

        string body = await new StreamReader(req.Body).ReadToEndAsync();
        var payload = JsonSerializer.Deserialize<StatusUpdatePayload>(body, JsonOpts);

        if (payload?.Status is null)
            return new BadRequestObjectResult(new { error = "Request body must include 'status'." });

        var allowed = new[] { "Pending", "Registered", "Verified", "Rejected" };
        if (!allowed.Contains(payload.Status))
            return new BadRequestObjectResult(new { error = $"Status must be one of: {string.Join(", ", allowed)}" });

        try
        {
            var record = await service.UpdateStatusAsync(guid, payload.Status);
            logger.LogInformation("Status updated for {Id} → {Status}", id, payload.Status);
            return new OkObjectResult(record);
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundObjectResult(new { error = $"Record {id} not found." });
        }
    }

    [Function("DeleteRecord")]
    public async Task<IActionResult> DeleteRecord(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "admin/records/{id}")] HttpRequest req,
        string id)
    {
        if (!Guid.TryParse(id, out var guid))
            return new BadRequestObjectResult(new { error = "Invalid record ID format." });

        try
        {
            await service.DeleteAsync(guid);
            logger.LogWarning("Record {Id} deleted via admin API", id);
            return new NoContentResult();
        }
        catch (KeyNotFoundException)
        {
            return new NotFoundObjectResult(new { error = $"Record {id} not found." });
        }
    }

    private record StatusUpdatePayload(string? Status);
}
