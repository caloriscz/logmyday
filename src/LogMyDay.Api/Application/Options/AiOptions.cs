namespace LogMyDay.Api.Application.Options;

public class AiOptions
{
    public const string SectionName = "AI";

    public string Provider { get; set; } = "openai";
    public string Model { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = string.Empty;
    public int MaxTokens { get; set; } = 1024;
    public float Temperature { get; set; } = 0.7f;
    public int MaxConversationMessages { get; set; } = 20;
    public bool Enabled { get; set; } = false;
}

