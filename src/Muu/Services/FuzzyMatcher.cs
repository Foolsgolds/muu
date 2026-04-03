namespace Muu.Services;

public static class FuzzyMatcher
{
    public static double Score(string query, string candidate)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(candidate))
            return -1;

        var queryLower = query.ToLowerInvariant();
        var candidateLower = candidate.ToLowerInvariant();

        int queryIndex = 0;
        int score = 0;
        int consecutive = 0;
        bool previousMatched = false;

        // First char bonus
        if (queryLower[0] == candidateLower[0])
            score += 15;

        for (int i = 0; i < candidateLower.Length && queryIndex < queryLower.Length; i++)
        {
            if (candidateLower[i] == queryLower[queryIndex])
            {
                score += 1;

                // Consecutive match bonus
                if (previousMatched)
                {
                    consecutive++;
                    score += consecutive * 5;
                }
                else
                {
                    consecutive = 0;
                }

                // Separator / camelCase boundary bonus
                if (i > 0)
                {
                    char prev = candidate[i - 1];
                    if (prev == ' ' || prev == '-' || prev == '_' || prev == '\\' || prev == '/')
                        score += 10;
                    else if (char.IsLower(prev) && char.IsUpper(candidate[i]))
                        score += 8;
                }

                previousMatched = true;
                queryIndex++;
            }
            else
            {
                previousMatched = false;
                consecutive = 0;
                score -= 1;
            }
        }

        // All query chars must be found
        if (queryIndex < queryLower.Length)
            return -1;

        // Normalize: theoretical max ~ 15 + sum of (1 + consecutive*5 + 10) per char
        double maxPossible = 15 + queryLower.Length * 16.0;
        return Math.Max(0, Math.Min(1, score / maxPossible));
    }
}
