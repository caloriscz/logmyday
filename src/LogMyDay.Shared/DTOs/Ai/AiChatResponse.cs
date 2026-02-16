namespace LogMyDay.Shared.DTOs.Ai;

public record AiChatResponse(string Message, List<AiSuggestedAction>? SuggestedActions = null);
