using System.Text;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 2; RND-DOM-16
/// <summary>Provides the parsing, merging and serialization of inline style strings as lists of declarations.</summary>
/// <remarks>
/// A style string is split on <c>;</c> outside parentheses and quotes, and each declaration <c>name: value</c> is trimmed.
/// Declarations are merged by name with first-seen order kept and the later value winning, and serialized as
/// <c>name: value; name: value;</c>.
/// </remarks>
internal static class StyleDeclarations
{
    public static string Merge(string ours, string theirs)
    {
        List<KeyValuePair<string, string>> declarations = new(8);
        Parse(ours, declarations);
        Parse(theirs, declarations);
        return Serialize(declarations);
    }

    public static void Parse(string css, List<KeyValuePair<string, string>> into)
    {
        int start = 0;
        int depth = 0;
        char quote = '\0';
        for (int i = 0; i <= css.Length; i++)
        {
            char c = i < css.Length ? css[i] : ';';
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '"':
                case '\'':
                    quote = c;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth = Math.Max(0, depth - 1);
                    break;
                case ';' when depth == 0:
                    AddDeclaration(css.AsSpan(start, i - start), into);
                    start = i + 1;
                    break;
            }
        }
    }

    public static string Serialize(List<KeyValuePair<string, string>> declarations)
    {
        StringBuilder result = new(declarations.Count * 24);
        for (int i = 0; i < declarations.Count; i++)
        {
            if (i > 0)
            {
                result.Append(' ');
            }

            result.Append(declarations[i].Key).Append(": ").Append(declarations[i].Value).Append(';');
        }

        return result.ToString();
    }

    private static void AddDeclaration(ReadOnlySpan<char> declaration, List<KeyValuePair<string, string>> into)
    {
        int colon = IndexOfColon(declaration);
        if (colon < 0)
        {
            return;
        }

        ReadOnlySpan<char> name = declaration[..colon].Trim();
        ReadOnlySpan<char> value = declaration[(colon + 1)..].Trim();
        if (name.IsEmpty)
        {
            return;
        }

        for (int i = 0; i < into.Count; i++)
        {
            if (name.SequenceEqual(into[i].Key))
            {
                into[i] = new KeyValuePair<string, string>(into[i].Key, value.ToString());
                return;
            }
        }

        into.Add(new KeyValuePair<string, string>(name.ToString(), value.ToString()));
    }

    // The first ':' outside quotes and parentheses; a custom property name (--x) or a value such as url("a:b") never splits.
    private static int IndexOfColon(ReadOnlySpan<char> declaration)
    {
        int depth = 0;
        char quote = '\0';
        for (int i = 0; i < declaration.Length; i++)
        {
            char c = declaration[i];
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            switch (c)
            {
                case '"':
                case '\'':
                    quote = c;
                    break;
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ':' when depth == 0:
                    return i;
            }
        }

        return -1;
    }
}
