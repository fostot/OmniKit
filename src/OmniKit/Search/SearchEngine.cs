using System.Text.RegularExpressions;

namespace OmniKit.Search;

public sealed record SearchEntry(string Id, string Title, string Description, string Keywords,
    string Source, Action Activate, uint IconId = 0);

public static partial class SearchEngine
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "i", "me", "my", "a", "an", "the", "to", "please", "can", "could", "you", "would",
        "want", "need", "show", "open", "launch", "find", "looking", "for", "where", "is", "are", "do", "how", "new",
    };

    public static string Normalize(string value) => Spaces().Replace(value.ToLowerInvariant(), " ").Trim();

    public static IReadOnlyList<SearchEntry> Find(IEnumerable<SearchEntry> entries, string query, int limit = 30)
    {
        var normalized = Normalize(query);
        var terms = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(t => !StopWords.Contains(t)).Distinct().ToArray();
        if (terms.Length == 0)
            return entries.Take(limit).ToArray();

        return entries.Select(entry => (Entry: entry, Score: Score(entry, normalized, terms)))
            .Where(result => result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit).Select(result => result.Entry).ToArray();
    }

    private static int Score(SearchEntry entry, string query, string[] terms)
    {
        var title = Normalize(entry.Title);
        var words = Normalize(entry.Title + " " + entry.Keywords).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var score = title == query ? 200 : title.Contains(query, StringComparison.Ordinal) ? 90 : 0;
        foreach (var term in terms)
        {
            if (words.Contains(term)) score += title.Split(' ').Contains(term) ? 30 : 20;
            else if (words.Any(word => word.StartsWith(term, StringComparison.Ordinal))) score += 12;
            else if (term.Length >= 4 && words.Any(word => OneEditAway(term, word))) score += 5;
            else return 0;
        }
        return score;
    }

    private static bool OneEditAway(string left, string right)
    {
        if (Math.Abs(left.Length - right.Length) > 1) return false;
        var a = 0; var b = 0; var edits = 0;
        while (a < left.Length && b < right.Length)
        {
            if (left[a] == right[b]) { a++; b++; continue; }
            if (++edits > 1) return false;
            if (left.Length >= right.Length) a++;
            if (right.Length >= left.Length) b++;
        }
        return edits + (left.Length - a) + (right.Length - b) <= 1;
    }

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex Spaces();
}
