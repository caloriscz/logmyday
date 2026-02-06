using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Shared.DTOs.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LogMyDay.Api.Application.Services.Ai;

public class AiAssistantService : IAiAssistantService
{
    private readonly IChatClient? _chatClient;
    private readonly AiOptions _options;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        IOptions<AiOptions> options,
        ILogger<AiAssistantService> logger,
        IChatClient? chatClient = null)
    {
        _options = options.Value;
        _logger = logger;
        _chatClient = chatClient;
    }

    public Task<bool> IsAvailable()
    {
        var available = _options.Enabled
            && _chatClient is not null
            && !string.IsNullOrWhiteSpace(_options.ApiKey);

        return Task.FromResult(available);
    }

    public async Task<AiChatResponse> Chat(AiChatRequest request, Guid userId)
    {
        if (!await IsAvailable())
        {
            return new AiChatResponse("The AI assistant is not currently available. Please contact your administrator to enable it.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new AiChatResponse("Please enter a message to get started.");
        }

        _logger.LogInformation("AI chat request from user {UserId}, message length: {Length}",
            userId, request.Message.Length);

        var messages = BuildChatMessages(request);

        var completionOptions = new ChatOptions
        {
            MaxOutputTokens = _options.MaxTokens,
            Temperature = _options.Temperature
        };

        var response = await _chatClient!.GetResponseAsync(messages, completionOptions);

        var assistantMessage = response.Text is { Length: > 0 } text
            ? text
            : "I'm sorry, I couldn't generate a response. Please try again.";

        var suggestedActions = ExtractSuggestedActions(assistantMessage);

        _logger.LogInformation("AI chat response for user {UserId}, response length: {Length}, actions: {ActionCount}",
            userId, assistantMessage.Length, suggestedActions?.Count ?? 0);

        return new AiChatResponse(assistantMessage, suggestedActions);
    }

    private static List<ChatMessage> BuildChatMessages(AiChatRequest request)
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, AiPrompts.GetSystemPrompt())
        };

        if (request.ConversationHistory is { Count: > 0 })
        {
            foreach (var historyMessage in request.ConversationHistory)
            {
                var role = historyMessage.Role.ToLowerInvariant() switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };
                messages.Add(new ChatMessage(role, historyMessage.Content));
            }
        }

        messages.Add(new ChatMessage(ChatRole.User, request.Message));

        return messages;
    }

    private static List<AiSuggestedAction>? ExtractSuggestedActions(string message)
    {
        var actions = new List<AiSuggestedAction>();

        var knownRoutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["/activities"] = "Activities",
            ["/tags"] = "Tags",
            ["/tags/new"] = "Create Tag",
            ["/option-lists"] = "Tag Options",
            ["/units"] = "Units",
            ["/insights"] = "Insights",
            ["/calendar"] = "Calendar",
            ["/calendar-linear"] = "Linear Calendar",
            ["/insights/journal"] = "Journal",
            ["/statistics"] = "Statistics",
            ["/charts"] = "Charts",
            ["/reports"] = "Reports",
            ["/backup"] = "Backup",
            ["/profile"] = "Profile",
            ["/"] = "Home"
        };

        foreach (var (route, label) in knownRoutes)
        {
            if (route == "/")
            {
                continue;
            }

            if (message.Contains(route, StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new AiSuggestedAction(label, route));
            }
        }

        return actions.Count > 0 ? actions : null;
    }
}
