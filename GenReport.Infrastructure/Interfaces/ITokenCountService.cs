using GenReport.Infrastructure.Models.HttpResponse.Core.Chat;

namespace GenReport.Infrastructure.Interfaces
{
    public interface ITokenCountService
    {
        Task<TokenCountResponse> GetSessionTokenCountAsync(long sessionId, CancellationToken ct = default);
    }
}
