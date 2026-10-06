using System.Windows.Media;
using Pouchy.Services;
using Xunit;

namespace Pouchy.Tests
{
    public class ColorParsingTests
    {
        [Theory]
        [InlineData("#F80", 255, 255, 136, 0)]
        [InlineData("#FF8800", 255, 255, 136, 0)]
        [InlineData("#80FF8800", 128, 255, 136, 0)]
        [InlineData("rgb(255, 136, 0)", 255, 255, 136, 0)]
        [InlineData("RGBA(10,20,30,0.5)", 128, 10, 20, 30)]
        [InlineData("rgba(10, 20, 30, 50%)", 128, 10, 20, 30)]
        public void TryParseColor_Valid(string text, byte a, byte r, byte g, byte b)
        {
            Assert.True(TextTools.TryParseColor(text, out var color));
            Assert.Equal(Color.FromArgb(a, r, g, b), color);
        }

        [Theory]
        [InlineData("#GGGGGG")]
        [InlineData("#12345")]
        [InlineData("rgb(300, 0, 0)")]
        [InlineData("rgba(1, 2, 3, 2)")]
        [InlineData("red")]
        [InlineData("the colour #FF8800")]
        public void TryParseColor_Invalid(string text)
        {
            Assert.False(TextTools.TryParseColor(text, out _));
        }

        [Fact]
        public void Conversions()
        {
            var orange = Color.FromRgb(255, 136, 0);
            Assert.Equal("#FF8800", TextTools.ToHex(orange));
            Assert.Equal("rgb(255, 136, 0)", TextTools.ToRgb(orange));
            Assert.Equal("hsl(32, 100%, 50%)", TextTools.ToHsl(orange));
            Assert.Equal("rgba(0, 0, 0, 0.5)", TextTools.ToRgb(Color.FromArgb(128, 0, 0, 0)));
        }
    }

    public class LinkPreviewParserTests
    {
        private static readonly Uri Page = new("https://www.example.com/blog/post");

        [Fact]
        public void PrefersOgTitle_AndDecodesEntities()
        {
            const string html = """
                <html><head>
                <title>Fallback</title>
                <meta property="og:title" content="Tom &amp; Jerry   Show">
                </head></html>
                """;
            Assert.Equal("Tom & Jerry Show", LinkPreviewParser.Parse(html, Page).Title);
        }

        [Fact]
        public void FallsBackToTitleTag()
        {
            Assert.Equal("My Page", LinkPreviewParser.Parse("<TITLE>\n  My   Page\n</TITLE>", Page).Title);
            Assert.Null(LinkPreviewParser.Parse("<p>no title</p>", Page).Title);
        }

        [Fact]
        public void Icon_PrefersTouchIcon_ResolvesRelative_SkipsSvg()
        {
            const string html = """
                <link rel="icon" href="/favicon.svg">
                <link rel="shortcut icon" href="/static/favicon.png">
                <link rel="apple-touch-icon" href="touch.png">
                """;
            var (_, icon) = LinkPreviewParser.Parse(html, Page);
            Assert.Equal("https://www.example.com/blog/touch.png", icon!.ToString());

            var (_, plain) = LinkPreviewParser.Parse("""<link href="/i.png" rel="icon">""", Page);
            Assert.Equal("https://www.example.com/i.png", plain!.ToString());
        }

        [Theory]
        [InlineData("https://www.example.com/a", "example.com")]
        [InlineData("http://docs.github.com", "docs.github.com")]
        [InlineData("not a url", "not a url")]
        public void HostName(string url, string expected)
        {
            Assert.Equal(expected, LinkPreviewParser.HostName(url));
        }
    }
}

namespace Pouchy.Tests
{
    public class UpdateServiceTests
    {
        [Theory]
        [InlineData("v1.2.3", 1, 2, 3)]
        [InlineData("1.0.0", 1, 0, 0)]
        [InlineData("v2.0.0-beta.1", 2, 0, 0)]
        [InlineData("v1.4", 1, 4, 0)]
        public void TryParseVersion_Valid(string text, int major, int minor, int patch)
        {
            Assert.True(Pouchy.Services.UpdateService.TryParseVersion(text, out var version));
            Assert.Equal(new Version(major, minor, patch), version);
        }

        [Theory]
        [InlineData("")]
        [InlineData("latest")]
        [InlineData("vX.Y")]
        public void TryParseVersion_Invalid(string text)
        {
            Assert.False(Pouchy.Services.UpdateService.TryParseVersion(text, out _));
        }

        [Fact]
        public void CurrentVersion_ComesFromProject()
        {
            Assert.Matches(@"^\d+\.\d+\.\d+$", Pouchy.Services.UpdateService.CurrentVersionText);
            Assert.True(Pouchy.Services.UpdateService.CurrentVersion >= new Version(1, 0, 0));
        }
    }
}
