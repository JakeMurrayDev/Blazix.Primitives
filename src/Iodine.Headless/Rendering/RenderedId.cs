using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

namespace Iodine.Headless.Rendering;

// RND-DOM-32 precedence (1)–(3) as an owner reads it (internals/resolveRenderedId.ts:18-27). RND-DOM-54 names the parts that
// publish the id that lands rather than the one they compute: Menu's popup and filterable list (issues 244 and 250, M6 track
// 10), and Combobox's popup by its 2026-11-11 note (CMB-DOM-24). A RenderFunction substitute is opaque to C#; its id is the one
// it was handed. Moved from ComboboxParts (M6 track 10, 2026-11-24) so Menu reads the substitute root the same way.
/// <summary>Resolves the id that lands on a part's element, as an owner that references it reads it.</summary>
internal static class RenderedId
{
    /// <summary>Resolves the id a part's element renders with.</summary>
    /// <param name="render">The part's <c>Render</c> fragment, or <see langword="null"/>.</param>
    /// <param name="consumerAttributes">The part's unmatched attributes, or <see langword="null"/>.</param>
    /// <param name="fallback">The part's generated id, or <see langword="null"/>.</param>
    /// <returns>
    /// The substitute element's own <c>id</c> when it has one, including the empty string; else the consumer's <c>id</c>
    /// string, including the empty string; else <paramref name="fallback"/>.
    /// </returns>
    public static string? Resolve(RenderFragment? render, IReadOnlyDictionary<string, object>? consumerAttributes, string? fallback)
    {
        if (render is not null && SubstituteRoot(render).Id is { } own)
        {
            return own;
        }

        return ConsumerId(consumerAttributes) ?? fallback;
    }

    /// <summary>Reads the consumer's <c>id</c> string from its attributes.</summary>
    /// <param name="consumerAttributes">The part's unmatched attributes, or <see langword="null"/>.</param>
    /// <returns>The id, including the empty string; <see langword="null"/> when the consumer gave none or gave <see langword="null"/>.</returns>
    public static string? ConsumerId(IReadOnlyDictionary<string, object>? consumerAttributes) =>
        consumerAttributes is not null && consumerAttributes.TryGetValue("id", out object? supplied) ? supplied as string : null;

    /// <summary>Reads the root element of a part's <c>Render</c> fragment: the first element frame, or the start tag of a markup block Razor folded.</summary>
    /// <param name="render">The part's <c>Render</c> fragment.</param>
    /// <returns>The root's tag name and its <c>id</c> attribute; both are <see langword="null"/> for a fragment whose root is a component.</returns>
    public static (string? TagName, string? Id) SubstituteRoot(RenderFragment render) => SubstituteRootAttribute(render, "id");

    /// <summary>Reads an attribute of the root element of a part's <c>Render</c> fragment.</summary>
    /// <param name="render">The part's <c>Render</c> fragment.</param>
    /// <param name="name">The attribute's name, such as <c>aria-label</c>.</param>
    /// <returns>The root's tag name and the attribute's string value; both are <see langword="null"/> for a fragment whose root is a component, and the value is <see langword="null"/> when the root has no such attribute.</returns>
    public static (string? TagName, string? Value) SubstituteRootAttribute(RenderFragment render, string name)
    {
        ArgumentNullException.ThrowIfNull(render);
        using RenderTreeBuilder builder = new();
        render(builder);
#pragma warning disable BL0006 // The frames are read through the public GetFrames(), as the render pipeline does (ADR 02 decision 1).
        ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
        for (int i = 0; i < frames.Count; i++)
        {
            RenderTreeFrame frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Element)
            {
                for (int j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                {
                    if (string.Equals(frames.Array[j].AttributeName, name, StringComparison.Ordinal))
                    {
                        return (frame.ElementName, frames.Array[j].AttributeValue as string);
                    }
                }

                return (frame.ElementName, null);
            }

            if (frame.FrameType == RenderTreeFrameType.Markup && !string.IsNullOrWhiteSpace(frame.MarkupContent))
            {
                if (!MarkupRootParser.TryParse(frame.MarkupContent, out MarkupRootParser.Root root, out _))
                {
                    return (null, null);
                }

                foreach (KeyValuePair<string, string> attribute in root.Attributes)
                {
                    if (string.Equals(attribute.Key, name, StringComparison.Ordinal))
                    {
                        return (root.TagName, attribute.Value);
                    }
                }

                return (root.TagName, null);
            }

            if (frame.FrameType == RenderTreeFrameType.Component)
            {
                return (null, null);
            }
        }
#pragma warning restore BL0006

        return (null, null);
    }
}
