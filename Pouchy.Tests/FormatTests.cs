using Pouchy.Helpers;
using Xunit;

namespace Pouchy.Tests
{
    public class FormatTests
    {
        [Theory]
        [InlineData(0, "0 B")]
        [InlineData(-5, "0 B")]
        [InlineData(512, "512 B")]
        [InlineData(1024, "1 KB")]
        [InlineData(1536, "1.5 KB")]
        [InlineData(5L * 1024 * 1024, "5 MB")]
        [InlineData(long.MaxValue, "8192 PB")]
        public void Size(long bytes, string expected)
        {
            using var _ = new CultureScope("en-US");
            Assert.Equal(expected, Format.Size(bytes));
        }

        [Theory]
        [InlineData(1, "1 item")]
        [InlineData(0, "0 items")]
        [InlineData(3, "3 items")]
        public void Plural(int count, string expected)
        {
            Assert.Equal(expected, Format.Plural(count, "item"));
        }

        private sealed class CultureScope : IDisposable
        {
            private readonly System.Globalization.CultureInfo _previous = System.Globalization.CultureInfo.CurrentCulture;

            public CultureScope(string name)
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);
            }

            public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = _previous;
        }
    }
}
