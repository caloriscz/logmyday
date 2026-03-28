using LogMyDay.Shared.Attributes;

namespace LogMyDay.Shared.Ai;

public record AiQuickQuestion(string Message, string DisplayText, ClientContext Context);

public static class AiQuickQuestions
{
    public static IReadOnlyList<AiQuickQuestion> All { get; } =
    [
        new("What features does LogMyDay have?", "What features are available?", ClientContext.All),
        new("How do I view my statistics?", "How do I view statistics?", ClientContext.All),
        new("How do tags work?", "How do tags work?", ClientContext.All),
        new("How do quick activities work?", "How do quick activities work?", ClientContext.All),
        new("How do I set up reminders for required activities?", "How do I set up reminders?", ClientContext.All),
        new("What are streaks and how do they work?", "What are streaks?", ClientContext.All),
        new("How do I manage my account settings?", "How do I manage my account?", ClientContext.All),
        new("How do I scan barcodes and QR codes?", "How do I scan barcodes?", ClientContext.Mobile),
        new("How do I create scan mappings?", "How do scan mappings work?", ClientContext.Mobile),
        new("How do I export my data?", "How do I export data?", ClientContext.Web),
        new("How do I create and restore backups?", "How do backups work?", ClientContext.Web),
        new("How does the calendar view work?", "How does the calendar work?", ClientContext.Web),
    ];

    public static List<AiQuickQuestion> GetRandom(ClientContext context, int count = 3)
    {
        var filtered = All
            .Where(q => q.Context == ClientContext.All || q.Context == context)
            .ToList();

        if (filtered.Count <= count)
        {
            return filtered;
        }

        var selected = new List<AiQuickQuestion>(count);
        var indices = new HashSet<int>();

        while (selected.Count < count)
        {
            var index = Random.Shared.Next(filtered.Count);
            if (indices.Add(index))
            {
                selected.Add(filtered[index]);
            }
        }

        return selected;
    }
}
