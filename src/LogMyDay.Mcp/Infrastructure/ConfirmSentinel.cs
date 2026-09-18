namespace LogMyDay.Mcp.Infrastructure;

/// <summary>
/// Guard for tools that destroy data. The agent must pass the exact sentinel the tool description
/// names; anything else throws before any work is done, which the error mapper turns into a
/// <c>confirmation-required</c> result carrying the expected value.
/// </summary>
public static class ConfirmSentinel
{
    public static void Require(string? confirm, string expected)
    {
        if (!string.Equals(confirm, expected, StringComparison.Ordinal))
        {
            throw new ConfirmationRequiredException(expected);
        }
    }
}
