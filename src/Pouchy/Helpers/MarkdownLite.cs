using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Pouchy.Helpers
{
    public enum InlineKind
    {
        Text,
        Bold,
        Code,
        Key,
        Link,
    }

    public sealed record InlinePart(InlineKind Kind, string Text, string? Url = null);

    /// <summary>
    /// Just enough Markdown for the changelog: ### headings, nested "- " lists, paragraphs,
    /// **bold**, `code`, &lt;kbd&gt; keys and [links](url). Other HTML is dropped.
    /// </summary>
    public static partial class MarkdownLite
    {
        [GeneratedRegex(@"\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`|<kbd>(?<key>.*?)</kbd>|\[(?<label>[^\]]+)\]\((?<url>[^)\s]+)\)|<[^>]+>")]
        private static partial Regex InlinePattern();

        /// <summary>Splits a line into styled pieces.</summary>
        public static List<InlinePart> ParseInline(string text)
        {
            var parts = new List<InlinePart>();
            int position = 0;
            foreach (Match match in InlinePattern().Matches(text))
            {
                if (match.Index > position) parts.Add(new InlinePart(InlineKind.Text, text[position..match.Index]));
                if (match.Groups["bold"].Success) parts.Add(new InlinePart(InlineKind.Bold, match.Groups["bold"].Value));
                else if (match.Groups["code"].Success) parts.Add(new InlinePart(InlineKind.Code, match.Groups["code"].Value));
                else if (match.Groups["key"].Success) parts.Add(new InlinePart(InlineKind.Key, match.Groups["key"].Value));
                else if (match.Groups["url"].Success) parts.Add(new InlinePart(InlineKind.Link, match.Groups["label"].Value.Replace("**", ""), match.Groups["url"].Value));
                // Any other HTML tag is skipped.
                position = match.Index + match.Length;
            }
            if (position < text.Length) parts.Add(new InlinePart(InlineKind.Text, text[position..]));

            // Merge neighbouring plain text (left behind by skipped tags).
            var merged = new List<InlinePart>();
            foreach (var part in parts)
            {
                if (part.Kind == InlineKind.Text && merged.Count > 0 && merged[^1].Kind == InlineKind.Text)
                {
                    merged[^1] = merged[^1] with { Text = merged[^1].Text + part.Text };
                }
                else if (part.Text.Length > 0)
                {
                    merged.Add(part);
                }
            }
            return merged;
        }

        /// <summary>Renders Markdown into blocks styled with the pouch theme. Links call <paramref name="openLink"/>.</summary>
        public static List<Block> Render(string markdown, Action<string> openLink)
        {
            var blocks = new List<Block>();
            var lists = new Stack<(List List, int Indent)>();
            ListItem? lastItem = null;
            Paragraph? paragraph = null;

            foreach (string raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.TrimEnd();
                string trimmed = line.TrimStart();
                int indent = line.Length - trimmed.Length;

                if (trimmed.Length == 0)
                {
                    paragraph = null;
                    continue;
                }

                if (trimmed.StartsWith('#'))
                {
                    lists.Clear();
                    lastItem = null;
                    paragraph = null;
                    var heading = new Paragraph { Margin = new Thickness(0, blocks.Count == 0 ? 0 : 12, 0, 4), FontWeight = FontWeights.SemiBold, FontSize = 13.5 };
                    heading.SetResourceReference(TextElement.ForegroundProperty, "Pouch.Text");
                    heading.SetResourceReference(TextElement.FontFamilyProperty, "Pouch.HeaderFontFamily");
                    AddInlines(heading.Inlines, trimmed.TrimStart('#').Trim(), openLink);
                    blocks.Add(heading);
                    continue;
                }

                if (trimmed.StartsWith("- ") || trimmed.StartsWith("* "))
                {
                    paragraph = null;
                    while (lists.Count > 0 && lists.Peek().Indent > indent) lists.Pop();
                    if (lists.Count == 0 || lists.Peek().Indent < indent)
                    {
                        var list = new List { MarkerStyle = TextMarkerStyle.Disc, Margin = new Thickness(0, 0, 0, lists.Count == 0 ? 6 : 0), Padding = new Thickness(18, 0, 0, 0) };
                        if (lists.Count == 0 || lastItem == null) blocks.Add(list);
                        else lastItem.Blocks.Add(list);
                        lists.Push((list, indent));
                    }
                    var item = new ListItem();
                    var text = new Paragraph { Margin = new Thickness(0, 1, 0, 3) };
                    AddInlines(text.Inlines, trimmed[2..], openLink);
                    item.Blocks.Add(text);
                    lists.Peek().List.ListItems.Add(item);
                    lastItem = item;
                    continue;
                }

                if (lastItem != null && indent > 0 && lastItem.Blocks.LastBlock is Paragraph continuation)
                {
                    // A wrapped list line.
                    continuation.Inlines.Add(new Run(" "));
                    AddInlines(continuation.Inlines, trimmed, openLink);
                    continue;
                }

                lists.Clear();
                lastItem = null;
                if (paragraph == null)
                {
                    paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                    blocks.Add(paragraph);
                }
                else
                {
                    paragraph.Inlines.Add(new Run(" "));
                }
                AddInlines(paragraph.Inlines, trimmed, openLink);
            }
            return blocks;
        }

        private static void AddInlines(InlineCollection inlines, string text, Action<string> openLink)
        {
            foreach (var part in ParseInline(text))
            {
                switch (part.Kind)
                {
                    case InlineKind.Bold:
                        var bold = new Bold();
                        bold.SetResourceReference(TextElement.ForegroundProperty, "Pouch.Text");
                        bold.Inlines.Add(new Run(part.Text));
                        inlines.Add(bold);
                        break;
                    case InlineKind.Code:
                        var code = new Run(part.Text);
                        code.SetResourceReference(TextElement.FontFamilyProperty, "Pouch.MonoFontFamily");
                        code.SetResourceReference(TextElement.BackgroundProperty, "Pouch.Tile");
                        inlines.Add(code);
                        break;
                    case InlineKind.Key:
                        var key = new Run($" {part.Text} ") { FontWeight = FontWeights.SemiBold, FontSize = 11 };
                        key.SetResourceReference(TextElement.BackgroundProperty, "Pouch.TileHover");
                        key.SetResourceReference(TextElement.ForegroundProperty, "Pouch.Text");
                        inlines.Add(key);
                        break;
                    case InlineKind.Link:
                        var link = new Hyperlink(new Run(part.Text)) { Cursor = System.Windows.Input.Cursors.Hand, TextDecorations = null };
                        link.SetResourceReference(TextElement.ForegroundProperty, "Pouch.Accent");
                        string url = part.Url!;
                        // Relative links (docs/ACTIONS.md) point into the GitHub repository.
                        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            url = "https://github.com/editorrylix/Pouchy/blob/main/" + url.TrimStart('/');
                        }
                        link.Click += (_, _) => openLink(url);
                        inlines.Add(link);
                        break;
                    default:
                        inlines.Add(new Run(part.Text));
                        break;
                }
            }
        }
    }
}
