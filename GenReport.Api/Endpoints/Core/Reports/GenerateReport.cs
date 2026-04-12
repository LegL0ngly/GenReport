using FastEndpoints;
using GenReport.Infrastructure.Models.HttpRequests.Core.Reports;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace GenReport.Api.Endpoints.Core.Reports
{
    /// <summary>
    /// Accepts a report-generation request and proxies it to the Go service,
    /// returning whatever the Go service responds with.
    /// </summary>
    public class GenerateReport(
        ILogger<GenerateReport> logger,
        ICurrentUserService currentUserService,
        IHttpClientFactory httpClientFactory) : Endpoint<GenerateReportRequest, HttpResponse<object>>
    {
        public override void Configure()
        {
            Post("/chat/generate-report");
        }

        public override async Task HandleAsync(GenerateReportRequest req, CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();

            try
            {
                var goPayload = new
                {
                    query                = req.Query,
                    databaseConnectionId = req.DatabaseConnectionId,
                    sessionId            = req.SessionId,
                    format               = req.Format,
                    userId               = userId.ToString()
                };

                var client = httpClientFactory.CreateClient("GoService");
                using var response = await client.PostAsJsonAsync("/reports/generate", goPayload, ct);

                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError(
                        "Go service report generation failed: {StatusCode} {Body}",
                        response.StatusCode, responseBody);

                    await SendAsync(new HttpResponse<object>(
                        response.StatusCode,
                        string.IsNullOrWhiteSpace(responseBody)
                            ? $"Report generation failed with status {response.StatusCode}"
                            : responseBody,
                        "ERR_REPORT_GENERATION_FAILED",
                        []), cancellation: ct);
                    return;
                }

           
                object jsonResult = default;
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    try
                    {
                        jsonResult = JsonSerializer.Deserialize<JsonElement>(responseBody);
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "Failed to parse Go service report response as JSON");
                        await SendAsync(new HttpResponse<object>(
                            HttpStatusCode.InternalServerError,
                            "Report generation succeeded but the response could not be parsed.",
                            "ERR_REPORT_PARSE_FAILED",
                            []), cancellation: ct);
                        return;
                    }
                }

                await SendAsync(new HttpResponse<object>(
                    jsonResult,
                    "Report generation initiated successfully.",
                    HttpStatusCode.OK), cancellation: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while proxying report generation request to Go service.");
                await SendAsync(new HttpResponse<object>(
                    HttpStatusCode.InternalServerError,
                    "An unexpected error occurred.",
                    "ERR_INTERNAL",
                    []), cancellation: ct);
            }
        }
    }
}
