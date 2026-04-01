using FastEndpoints;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.HttpResponse.Core.Chat;
using GenReport.Infrastructure.Models.Shared;
using GenReport.Services.Interfaces;
using System.Net;

namespace GenReport.Api.Endpoints.Core.Chat
{
    public class GetSessionTokenCount(ITokenCountService tokenCountService, ICurrentUserService currentUserService) : EndpointWithoutRequest<HttpResponse<TokenCountResponse>>
    {
        public override void Configure()
        {
            Get("/chat/sessions/{id}/tokens");
        }

        public override async Task HandleAsync(CancellationToken ct)
        {
            var id = Route<long>("id");
            var userId = currentUserService.LoggedInUserId();

            // Depending on the logic, we may first check ownership. For now the service assumes valid access
            // but production logic might pass userId to the service. We are strictly providing the token count.
            var response = await tokenCountService.GetSessionTokenCountAsync(id, ct);

            if (!response.IsSuccess)
            {
                await SendAsync(new HttpResponse<TokenCountResponse>(HttpStatusCode.NotFound, response.ErrorMessage ?? "Token count calculation failed.", "ERR_NOT_FOUND", []), cancellation: ct);
                return;
            }

            await SendAsync(new HttpResponse<TokenCountResponse>(response, "Token count fetched successfully", HttpStatusCode.OK), cancellation: ct);
        }
    }
}
