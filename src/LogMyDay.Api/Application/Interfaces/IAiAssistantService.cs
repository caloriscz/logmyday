using LogMyDay.Shared.DTOs.Ai;

namespace LogMyDay.Api.Application.Interfaces;

public interface IAiAssistantService
{
    Task<AiChatResponse> Chat(AiChatRequest request, Guid userId);

    Task<bool> IsAvailable();
}
