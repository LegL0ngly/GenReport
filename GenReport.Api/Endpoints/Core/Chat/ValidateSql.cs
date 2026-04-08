using FastEndpoints;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Models.HttpRequests.Core.Chat;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenReport.Api.Endpoints.Core.Chat
{
    public class ValidateSql(
        ApplicationDbContext context,
        ILogger<ValidateSql> logger,
        ICurrentUserService currentUserService,
        IHttpClientFactory httpClientFactory) : Endpoint<ValidateSqlRequest, HttpResponse<object>>
    {
        public override void Configure()
        {
            Post("/chat/messages/validate");
        }

        public override async Task HandleAsync(ValidateSqlRequest req, CancellationToken ct)
        {
            var userId = currentUserService.LoggedInUserId();
            var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                logger.LogError("Invalid user context");
                await SendAsync(new HttpResponse<object>(HttpStatusCode.Unauthorized, "Unauthorized", "ERR_UNAUTHORIZED", []), cancellation: ct);
                return;
            }

            if (!long.TryParse(req.DatabaseConnectionId, out var dbId))
            {
                await SendAsync(new HttpResponse<object>(HttpStatusCode.BadRequest, "Invalid database connection ID.", "ERR_BAD_REQUEST", []), cancellation: ct);
                return;
            }

            var db = await context.Databases.FirstOrDefaultAsync(d => d.Id == dbId, ct);
            if (db == null)
            {
                await SendAsync(new HttpResponse<object>(HttpStatusCode.NotFound, "Database not found.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            try
            {
                var goPayload = new 
                { 
                    databaseId = req.DatabaseConnectionId, 
                    sql = req.Query 
                };

                var client = httpClientFactory.CreateClient("GoService");
                using var response = await client.PostAsJsonAsync("/queries/validate", goPayload, ct);

                var responseBody = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    logger.LogError("Go service validation failed: {StatusCode} {Body}", response.StatusCode, responseBody);
                    
                    await SendAsync(new HttpResponse<object>(
                        response.StatusCode, 
                        string.IsNullOrWhiteSpace(responseBody) ? $"Query validation failed with status {response.StatusCode}" : responseBody,
                        "ERR_QUERY_VALIDATION_FAILED", 
                        []), cancellation: ct);
                    return;
                }
                
                object jsonResult = responseBody;
                if (!string.IsNullOrWhiteSpace(responseBody))
                {
                    try
                    {
                        jsonResult = JsonSerializer.Deserialize<object>(responseBody) ?? responseBody;
                    }
                    catch (JsonException ex)
                    {
                        logger.LogError(ex, "Failed to parse Go service response as JSON");
                    }
                }

                await SendAsync(new HttpResponse<object>(
                    jsonResult, 
                    "Query validated successfully.", 
                    HttpStatusCode.OK), cancellation: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while validating SQL via Go service.");
                await SendAsync(new HttpResponse<object>(
                    HttpStatusCode.InternalServerError, 
                    ex.Message, 
                    "ERR_INTERNAL", 
                    []), cancellation: ct);
            }
        }
    }
}
