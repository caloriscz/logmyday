using LogMyDay.Api.Application.Services.Ai;
using LogMyDay.Shared.DTOs.Ai;

namespace LogMyDay.Api.Application.Interfaces;

public interface IAiAssistantService
{
    Task<AiChatResult> Chat(AiChatRequest request, Guid userId);

    Task<bool> IsAvailable();
}
