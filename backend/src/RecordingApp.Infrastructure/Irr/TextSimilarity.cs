namespace RecordingApp.Infrastructure.Irr;

// Word-level agreement between two independent annotators' transcriptions of
// the same recording, for the admin IRR report. 1.0 = identical word
// sequence, 0.0 = completely different; based on word-level edit distance
// (the same idea as Word Error Rate) normalized by the longer transcription's
// length, so it's symmetric and doesn't require picking a "reference" rater.
public static class TextSimilarity
{
    public static double WordLevelSimilarity(string a, string b)
    {
        var wordsA = Tokenize(a);
        var wordsB = Tokenize(b);
        if (wordsA.Length == 0 && wordsB.Length == 0) return 1.0;

        var distance = WordEditDistance(wordsA, wordsB);
        var maxLen = Math.Max(wordsA.Length, wordsB.Length);
        return maxLen == 0 ? 1.0 : 1.0 - (double)distance / maxLen;
    }

    private static string[] Tokenize(string text) =>
        text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    private static int WordEditDistance(string[] a, string[] b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var substitutionCost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + substitutionCost);
            }
        }

        return dp[a.Length, b.Length];
    }
}
