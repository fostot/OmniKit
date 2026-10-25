namespace OmniKit.Companions;

/// <summary>Local selection only. Timing and enabled state remain with the existing companion controller.</summary>
internal sealed class SayingController(Func<IReadOnlyList<SayingEntry>> snapshot, Random? random = null)
{
    private readonly Random random = random ?? Random.Shared;
    private readonly Queue<(string Id, string Text)> recent = new();

    public bool IsAvailable(SayingEntry entry, string avatar) => snapshot().Any(current =>
        current.Id == entry.Id && current.Enabled && !current.Deleted && current.Text == entry.Text
        && (current.Companion == "Universal" || current.Companion == avatar));

    public SayingEntry? GetNext(string avatar, string context)
    {
        if (!SayingLibrary.Companions.Contains(avatar, StringComparer.Ordinal)
            || !SayingLibrary.Contexts.Contains(context, StringComparer.Ordinal)) return null;
        var eligible = snapshot().Where(entry => entry.Enabled && !entry.Deleted
            && (entry.Companion == "Universal" || entry.Companion == avatar)
            && (entry.Context == "Any" || entry.Context == context)).ToArray();
        if (eligible.Length == 0) return null;
        var available = Available(eligible);
        // A small user-filtered pool relaxes the oldest exclusion first, avoiding immediate repeats when possible.
        while (available.Length == 0 && recent.Count > 0) { recent.Dequeue(); available = Available(eligible); }
        var selected = available[random.Next(available.Length)];
        recent.Enqueue((selected.Id, selected.Text));
        while (recent.Count > 5) recent.Dequeue();
        return selected;
    }

    private SayingEntry[] Available(SayingEntry[] eligible) => eligible.Where(entry => !recent.Any(previous =>
        previous.Id == entry.Id || string.Equals(previous.Text, entry.Text, StringComparison.OrdinalIgnoreCase))).ToArray();
}
