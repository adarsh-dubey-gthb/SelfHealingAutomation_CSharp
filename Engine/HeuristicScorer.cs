namespace SelfHealingAutomation.Engine;

public static class HeuristicScorer
{
    public static double CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2))
            return 0.0;

        s1 = Normalize(s1);
        s2 = Normalize(s2);

        if (s1 == s2) return 1.0;
        if (s1.Contains(s2) || s2.Contains(s1)) return 0.85;

        // Token intersection (Jaccard similarity)
        var words1 = s1.Split(new[] { ' ', '-', '_', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = s2.Split(new[] { ' ', '-', '_', ':', '.' }, StringSplitOptions.RemoveEmptyEntries);
        var intersection = words1.Intersect(words2, StringComparer.OrdinalIgnoreCase).Count();
        var union = words1.Union(words2, StringComparer.OrdinalIgnoreCase).Count();

        double jaccard = union > 0 ? (double)intersection / union : 0;
        if (jaccard > 0.5) return jaccard;

        // Levenshtein distance similarity
        int maxLen = Math.Max(s1.Length, s2.Length);
        int dist = LevenshteinDistance(s1, s2);
        double levSim = 1.0 - ((double)dist / maxLen);

        return Math.Max(jaccard, Math.Max(0.0, levSim));
    }

    public static string Normalize(string input)
    {
        return input.Trim().ToLowerInvariant();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }
}

