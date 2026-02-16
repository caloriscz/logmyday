using LogMyDay.Shared.DTOs.Ai;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface IAiApi
{
    [Post("/api/ai/chat")]
    Task<AiChatResponse> Chat([Body] AiChatRequest request, CancellationToken cancellationToken = default);

    [Get("/api/ai/status")]
    Task<AiStatusResponse> GetStatus(CancellationToken cancellationToken = default);
}
