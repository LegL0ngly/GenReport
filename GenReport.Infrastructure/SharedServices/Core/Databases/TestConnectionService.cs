using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.HttpRequests.Core.Databases;
using System.Net.Http.Json;

namespace GenReport.Infrastructure.SharedServices.Core.Databases
{
    public class TestConnectionService(IHttpClientFactory httpClientFactory, IApplicationConfiguration appConfig) : ITestConnectionService
    {
        public async Task<(bool IsSuccess, string Message)> TestConnectionAsync(AddDatabaseRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var client = httpClientFactory.CreateClient("GoService");
                using var response = await client.PostAsJsonAsync(appConfig.GoTestConnectionPath, request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    if (!string.IsNullOrWhiteSpace(responseBody))
                    {
                        return (false, responseBody);
                    }

                    return (false, $"Go service returned {(int)response.StatusCode} {response.StatusCode}.");
                }

                return (true, string.IsNullOrWhiteSpace(responseBody) ? "Database connection test successful." : responseBody);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
