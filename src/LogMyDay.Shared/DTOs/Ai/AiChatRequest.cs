namespace LogMyDay.Shared.DTOs.Ai;

public record AiChatRequest(string Message, List<AiChatMessage> ConversationHistory);
