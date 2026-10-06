namespace Pouchy.Helpers
{
    public static class Format
    {
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB", "PB" };

        public static string Size(long bytes)
        {
            if (bytes <= 0) return "0 B";

            double value = bytes;
            int place = 0;
            while (value >= 1024 && place < SizeSuffixes.Length - 1)
            {
                value /= 1024;
                place++;
            }
            return place == 0 ? $"{bytes} B" : $"{Math.Round(value, 1)} {SizeSuffixes[place]}";
        }

        public static string Plural(int count, string singular) =>
            count == 1 ? $"1 {singular}" : $"{count} {singular}s";
    }
}
