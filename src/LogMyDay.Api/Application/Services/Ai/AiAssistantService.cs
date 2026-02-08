using System.Text.Json;
using LogMyDay.Api.Application.Interfaces;
using LogMyDay.Api.Application.Options;
using LogMyDay.Shared.DTOs.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace LogMyDay.Api.Application.Services.Ai;

public class AiAssistantService : IAiAssistantService
{
    private readonly IAiChatClientFactory _chatClientFactory;
    private readonly IRouteDiscoveryService _routeDiscovery;
    private readonly AiToolFunctions _toolFunctions;
    private readonly IOptionsMonitor<AiOptions> _optionsMonitor;
    private readonly ILogger<AiAssistantService> _logger;

    public AiAssistantService(
        IAiChatClientFactory chatClientFactory,
        IRouteDiscoveryService routeDiscovery,
        AiToolFunctions toolFunctions,
        IOptionsMonitor<AiOptions> optionsMonitor,
        ILogger<AiAssistantService> logger)
    {
        _chatClientFactory = chatClientFactory;
        _routeDiscovery = routeDiscovery;
        _toolFunctions = toolFunctions;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    public Task<bool> IsAvailable()
    {
        var available = _chatClientFactory.IsAvailable();

        return Task.FromResult(available);
    }

    public async Task<AiChatResult> Chat(AiChatRequest request, Guid userId)
    {
        if (!await IsAvailable())
        {
            return AiChatResult.Fail(AiErrorCode.Unavailable, "The AI assistant is not currently available. Please contact your administrator to enable it.");
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return AiChatResult.Fail(AiErrorCode.InvalidRequest, "Please enter a message to get started.");
        }

        try
        {
            _logger.LogInformation("AI chat request from user {UserId}, message length: {Length}",
                userId, request.Message.Length);

            var chatClient = _chatClientFactory.GetChatClient();
            if (chatClient is null)
            {
                _logger.LogWarning("Chat client unavailable for user {UserId}", userId);

                return AiChatResult.Fail(AiErrorCode.Unavailable, "AI is temporarily unavailable, please try again later.");
            }

            var messages = BuildChatMessages(request, userId);
            var aiOptions = _optionsMonitor.CurrentValue;
            var tools = BuildToolDefinitions();

            var chatOptions = new ChatCompletionOptions
            {
                MaxOutputTokenCount = aiOptions.MaxTokens,
                Temperature = aiOptions.Temperature
            };

            foreach (var tool in tools)
            {
                chatOptions.Tools.Add(tool);
            }

            // Function calling loop
            var maxIterations = 5;
            var iteration = 0;
            bool requiresAction;

            do
            {
                requiresAction = false;
                var completion = await chatClient.CompleteChatAsync(messages, chatOptions);
                var result = completion.Value;

                switch (result.FinishReason)
                {
                    case ChatFinishReason.Stop:
                    {
                        var assistantText = result.Content.Count > 0
                            ? result.Content[0].Text
                            : string.Empty;

                        _logger.LogInformation(
                            "AI response for user {UserId}: Length={Length}, FinishReason=Stop",
                            userId, assistantText?.Length ?? 0);

                        var assistantMessage = !string.IsNullOrEmpty(assistantText)
                            ? assistantText
                            : "I'm sorry, I couldn't generate a response. Please try again.";

                        var suggestedActions = ExtractSuggestedActions(assistantMessage);

                        if (result.Usage is not null)
                        {
                            _logger.LogInformation(
                                "AI tokens for user {UserId}: input={InputTokens}, output={OutputTokens}, total={TotalTokens}",
                                userId, result.Usage.InputTokenCount, result.Usage.OutputTokenCount,
                                result.Usage.TotalTokenCount);
                        }

                        return AiChatResult.Ok(assistantMessage, suggestedActions);
                    }

                    case ChatFinishReason.ToolCalls:
                    {
                        iteration++;
                        _logger.LogInformation(
                            "AI tool calls for user {UserId}, iteration {Iteration}, {ToolCount} call(s)",
                            userId, iteration, result.ToolCalls.Count);

                        // Add assistant message with tool calls to conversation
                        messages.Add(new AssistantChatMessage(result));

                        // Execute each tool call and add results
                        foreach (var toolCall in result.ToolCalls)
                        {
                            var toolResult = await ExecuteToolFunction(toolCall, userId);
                            messages.Add(new ToolChatMessage(toolCall.Id, toolResult));

                            _logger.LogInformation(
                                "Executed tool {FunctionName} for user {UserId}, result length: {Length}",
                                toolCall.FunctionName, userId, toolResult.Length);
                        }

                        if (iteration < maxIterations)
                        {
                            requiresAction = true;
                        }
                        else
                        {
                            _logger.LogWarning("Max tool iterations ({Max}) reached for user {UserId}",
                                maxIterations, userId);
                        }

                        break;
                    }

                    default:
                    {
                        _logger.LogWarning("Unexpected finish reason {Reason} for user {UserId}",
                            result.FinishReason, userId);

                        return AiChatResult.Fail(AiErrorCode.ProviderError, "I'm sorry, I couldn't generate a response. Please try again.");
                    }
                }
            } while (requiresAction);

            // If we exhausted iterations without a Stop response
            return AiChatResult.Fail(AiErrorCode.MaxIterationsExhausted, "I found the information but couldn't format a complete response. Please try again.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI chat request for user {UserId}", userId);

            return AiChatResult.Fail(AiErrorCode.ProviderError, "AI is temporarily unavailable, please try again later.");
        }
    }

    private async Task<string> ExecuteToolFunction(ChatToolCall toolCall, Guid userId)
    {
        try
        {
            object result = toolCall.FunctionName switch
            {
                "getTags" => await _toolFunctions.GetTags(userId),
                "getStatistics" => await ExecuteGetStatistics(toolCall, userId),
                "getChartTypes" => await _toolFunctions.GetChartTypes(),
                "getUnits" => await _toolFunctions.GetUnits(userId),
                "getOptionLists" => await _toolFunctions.GetOptionLists(userId),
                "getInputTypes" => await _toolFunctions.GetInputTypes(),
                _ => new { error = $"Unknown function: {toolCall.FunctionName}" }
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool function {FunctionName} for user {UserId}",
                toolCall.FunctionName, userId);

            return JsonSerializer.Serialize(new { error = $"Failed to execute {toolCall.FunctionName}" });
        }
    }

    private async Task<object> ExecuteGetStatistics(ChatToolCall toolCall, Guid userId)
    {
        int? tagId = null;

        if (toolCall.FunctionArguments is not null)
        {
            using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
            if (doc.RootElement.TryGetProperty("tagId", out var tagIdElement) &&
                tagIdElement.ValueKind == JsonValueKind.Number)
            {
                tagId = tagIdElement.GetInt32();
            }
        }

        return await _toolFunctions.GetStatistics(userId, tagId);
    }

    private List<OpenAI.Chat.ChatMessage> BuildChatMessages(AiChatRequest request, Guid userId)
    {
        var navigationMap = _routeDiscovery.GetNavigationMap();
        var systemPrompt = AiPrompts.GetSystemPrompt(navigationMap);

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        // Trim conversation history to stay within limits
        var aiOptions = _optionsMonitor.CurrentValue;
        var maxMessages = aiOptions.MaxConversationMessages;

        if (request.ConversationHistory is { Count: > 0 })
        {
            var historyToInclude = request.ConversationHistory.Count > maxMessages
                ? request.ConversationHistory.TakeLast(maxMessages).ToList()
                : request.ConversationHistory;

            foreach (var historyMessage in historyToInclude)
            {
                OpenAI.Chat.ChatMessage msg = historyMessage.Role.ToLowerInvariant() switch
                {
                    "assistant" => new AssistantChatMessage(historyMessage.Content),
                    _ => new UserChatMessage(historyMessage.Content)
                };
                messages.Add(msg);
            }

            if (request.ConversationHistory.Count > maxMessages)
            {
                _logger.LogInformation(
                    "Trimmed conversation history for user {UserId} from {Original} to {Trimmed} messages",
                    userId, request.ConversationHistory.Count, maxMessages);
            }
        }

        messages.Add(new UserChatMessage(request.Message));

        return messages;
    }

    private static List<ChatTool> BuildToolDefinitions()
    {
        return
        [
            ChatTool.CreateFunctionTool(
                functionName: "getTags",
                functionDescription: "Get a list of all tags for the current user, including their properties like input type, whether they are required, and time granularity."),

            ChatTool.CreateFunctionTool(
                functionName: "getStatistics",
                functionDescription: "Get aggregated statistics about the user's activities and tags, such as total counts and date ranges.",
                functionParameters: BinaryData.FromString("""
                    {
                        "type": "object",
                        "properties": {
                            "tagId": {
                                "type": "integer",
                                "description": "Optional tag ID to get statistics for a specific tag"
                            }
                        }
                    }
                    """)),

            ChatTool.CreateFunctionTool(
                functionName: "getChartTypes",
                functionDescription: "Get a list of available chart types that can be used to visualize numeric tag data."),

            ChatTool.CreateFunctionTool(
                functionName: "getUnits",
                functionDescription: "Get a list of measurement units available for numeric tags."),

            ChatTool.CreateFunctionTool(
                functionName: "getOptionLists",
                functionDescription: "Get a list of option lists (predefined value lists) available for tags."),

            ChatTool.CreateFunctionTool(
                functionName: "getInputTypes",
                functionDescription: "Get a list of available input types for tags, such as Integer, String, Boolean, Date, etc.")
        ];
    }

    private List<AiSuggestedAction>? ExtractSuggestedActions(string message)
    {
        var actions = new List<AiSuggestedAction>();
        var navigationMap = _routeDiscovery.GetNavigationMap();

        foreach (var route in navigationMap)
        {
            // Skip home route (/) to avoid false positives
            if (route.Path == "/")
            {
                continue;
            }

            if (message.Contains(route.Path, StringComparison.OrdinalIgnoreCase))
            {
                actions.Add(new AiSuggestedAction(route.Label, route.Path, route.Description));
            }
        }

        return actions.Count > 0 ? actions : null;
    }
}

