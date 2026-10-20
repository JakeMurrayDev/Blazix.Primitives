using System.Net;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 1
/// <summary>
/// Reads the single root element of a static markup block. The Razor compiler folds a substitute element with no
/// dynamic parts (<c>&lt;Render&gt;&lt;div class="x"&gt;…&lt;/div&gt;&lt;/Render&gt;</c>) into one markup frame, so the
/// frame rewrite parses the open tag for the attributes to merge and keeps the inner markup as
/// one markup child. Only what Razor emits for static HTML is handled: a start tag with quoted, unquoted or valueless
/// attributes, a self-closing or void element, or a matching end tag that closes the root at the end of the block.
/// </summary>
internal static class MarkupRootParser
{
    private static readonly HashSet<string> s_voidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr",
    };

    public readonly record struct Root(string TagName, List<KeyValuePair<string, string>> Attributes, string? InnerMarkup);

    public static bool TryParse(string markup, out Root root, out string reason)
    {
        root = default;
        ReadOnlySpan<char> text = markup.AsSpan().Trim();
        if (text.Length < 3 || text[0] != '<' || !char.IsLetter(text[1]))
        {
            reason = "the markup does not start with an element";
            return false;
        }

        int i = 1;
        int nameStart = i;
        while (i < text.Length && IsNameChar(text[i]))
        {
            i++;
        }

        string tagName = text[nameStart..i].ToString();
        List<KeyValuePair<string, string>> attributes = [];
        bool selfClosing = false;
        while (true)
        {
            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            if (i >= text.Length)
            {
                reason = "the start tag is not closed";
                return false;
            }

            if (text[i] == '>')
            {
                i++;
                break;
            }

            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>')
            {
                selfClosing = true;
                i += 2;
                break;
            }

            int attributeStart = i;
            while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '=' && text[i] != '>' && !(text[i] == '/' && i + 1 < text.Length && text[i + 1] == '>'))
            {
                i++;
            }

            if (i == attributeStart)
            {
                reason = "an attribute name could not be read";
                return false;
            }

            string attributeName = text[attributeStart..i].ToString();
            string attributeValue = string.Empty;
            int afterName = i;
            while (afterName < text.Length && char.IsWhiteSpace(text[afterName]))
            {
                afterName++;
            }

            if (afterName < text.Length && text[afterName] == '=')
            {
                i = afterName + 1;
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i < text.Length && (text[i] == '"' || text[i] == '\''))
                {
                    char quote = text[i];
                    int valueStart = ++i;
                    while (i < text.Length && text[i] != quote)
                    {
                        i++;
                    }

                    if (i >= text.Length)
                    {
                        reason = "an attribute value is not closed";
                        return false;
                    }

                    attributeValue = WebUtility.HtmlDecode(text[valueStart..i].ToString());
                    i++;
                }
                else
                {
                    int valueStart = i;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '>')
                    {
                        i++;
                    }

                    attributeValue = WebUtility.HtmlDecode(text[valueStart..i].ToString());
                }
            }

            attributes.Add(new KeyValuePair<string, string>(attributeName, attributeValue));
        }

        ReadOnlySpan<char> rest = text[i..];
        if (selfClosing || s_voidElements.Contains(tagName))
        {
            if (!rest.IsWhiteSpace())
            {
                reason = "the markup has more than one root";
                return false;
            }

            root = new Root(tagName, attributes, null);
            reason = string.Empty;
            return true;
        }

        if (!TryFindClosingTag(rest, tagName, out int innerLength, out int closeLength))
        {
            reason = "the root element is not closed";
            return false;
        }

        if (!rest[(innerLength + closeLength)..].IsWhiteSpace())
        {
            reason = "the markup has more than one root";
            return false;
        }

        root = new Root(tagName, attributes, rest[..innerLength].ToString());
        reason = string.Empty;
        return true;
    }

    // Walks tags of the root's name to find the end tag that brings the nesting depth back to zero.
    private static bool TryFindClosingTag(ReadOnlySpan<char> content, string tagName, out int innerLength, out int closeLength)
    {
        int depth = 1;
        int i = 0;
        while (i < content.Length)
        {
            if (content[i] != '<')
            {
                i++;
                continue;
            }

            if (content[i..].StartsWith("<!--", StringComparison.Ordinal))
            {
                int end = content[i..].IndexOf("-->", StringComparison.Ordinal);
                i = end < 0 ? content.Length : i + end + 3;
                continue;
            }

            bool closing = i + 1 < content.Length && content[i + 1] == '/';
            int nameStart = closing ? i + 2 : i + 1;
            int nameEnd = nameStart;
            while (nameEnd < content.Length && IsNameChar(content[nameEnd]))
            {
                nameEnd++;
            }

            bool sameName = nameEnd > nameStart && content[nameStart..nameEnd].Equals(tagName, StringComparison.OrdinalIgnoreCase);
            int tagEnd = IndexOfTagEnd(content, nameEnd);
            if (tagEnd < 0)
            {
                break;
            }

            if (sameName)
            {
                if (closing)
                {
                    depth--;
                    if (depth == 0)
                    {
                        innerLength = i;
                        closeLength = tagEnd + 1 - i;
                        return true;
                    }
                }
                else if (content[tagEnd - 1] != '/')
                {
                    depth++;
                }
            }

            i = tagEnd + 1;
        }

        innerLength = 0;
        closeLength = 0;
        return false;
    }

    private static int IndexOfTagEnd(ReadOnlySpan<char> content, int from)
    {
        char quote = '\0';
        for (int i = from; i < content.Length; i++)
        {
            char c = content[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
            }
            else if (c == '"' || c == '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '-' || c == ':' || c == '_';
}
