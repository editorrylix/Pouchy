namespace Pouchy.Helpers
{
    /// <summary>Ranks command palette entries against what was typed.</summary>
    public static class FuzzyMatch
    {
        /// <summary>
        /// A score above zero if every typed character appears in order in the text. Whole-word and
        /// word-start matches score highest, so "ns" finds "New shelf" before "Copy paths".
        /// Several words each have to match.
        /// </summary>
        /// <param name="scattered">Also accept letters spread through the text, not only whole words.</param>
        public static int Score(string query, string text, bool scattered = true)
        {
            var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return 1;

            int total = 0;
            foreach (var word in words)
            {
                int score = ScoreWord(word, text, scattered);
                if (score <= 0) return 0;
                total += score;
            }
            return total;
        }

        private static int ScoreWord(string word, string text, bool scattered)
        {
            int index = text.IndexOf(word, StringComparison.CurrentCultureIgnoreCase);
            if (index >= 0)
            {
                bool atWordStart = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
                return (atWordStart ? 100 : 60) + (index == 0 ? 20 : 0) - Math.Min(index, 20);
            }

            if (!scattered) return 0;

            // Subsequence: each typed letter in order, with a bonus for letters that start words.
            int score = 0;
            int position = 0;
            foreach (char c in word)
            {
                int found = -1;
                for (int i = position; i < text.Length; i++)
                {
                    if (char.ToLowerInvariant(text[i]) == char.ToLowerInvariant(c))
                    {
                        found = i;
                        break;
                    }
                }
                if (found < 0) return 0;
                bool wordStart = found == 0 || !char.IsLetterOrDigit(text[found - 1]);
                score += wordStart ? 8 : found == position ? 4 : 1;
                position = found + 1;
            }
            return score;
        }
    }
}
