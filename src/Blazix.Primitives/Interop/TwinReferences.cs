using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-27 (element-typed option values cross as interop arguments; settled by the orchestrator
// 2026-10-27, M4 track 1). The options record crosses as one JSON string, so an element inside it would never reach the
// module as an element; the references cross beside it as one interop argument, which Blazor revives, and the options
// carry each reference's index in an `int?` field the module names (null when the option is absent): `ContainerRef` on the
// portal's options, `AnchorRef` and `BoundaryRefs` on positioning's (track 2), the focus manager's element options (track 4).
// The module reads one with `resolveReference(references, options.containerRef)` from `js/twin.ts`.
/// <summary>
/// Collects the elements and script-held objects that one <c>attach</c> or <c>update</c> sends to a module after its
/// options; each option that names one of them carries the index <see cref="Add(ElementReference)"/> or
/// <see cref="Add(IJSObjectReference)"/> returned.
/// </summary>
internal sealed class TwinReferences
{
    private List<object>? references;
    private List<string?>? captures;
    private bool hasElement;

    /// <summary>Gets the number of references collected.</summary>
    /// <value>The count; 0 for a new instance.</value>
    public int Count => references?.Count ?? 0;

    /// <summary>Adds an element.</summary>
    /// <param name="element">The element, revived by Blazor as the DOM element in the module.</param>
    /// <returns>The index the option that names the element carries.</returns>
    public int Add(ElementReference element)
    {
        // BLZX2005: the references array holds elements beside script-held objects, so its element type is object, and
        // Blazor's interop serializes each entry by its runtime type, as it does the entries of an invocation's own
        // argument array. This is the array's one boxing site; it runs once per attach or update that names an element.
#pragma warning disable BLZX2005
        return Append(element, element.Id);
#pragma warning restore BLZX2005
    }

    /// <summary>Adds an object a script holds, such as a closed shadow root.</summary>
    /// <param name="reference">The reference, revived by Blazor as the object itself in the module.</param>
    /// <returns>The index the option that names the object carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    public int Add(IJSObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Append(reference, captureId: null);
    }

    /// <summary>Gets the interop argument that follows the options string.</summary>
    /// <returns>A new array of the references in the order they were added, or <see langword="null"/> when there are none.</returns>
    internal object[]? ToArgument() => references is { Count: > 0 } ? [.. references] : null;

    // ADR 04 Amendments 2026-10-28 (design point 9 of M4 track 8): Blazor revives an element with a document query, which
    // pierces no shadow root, so the module finds an element inside one by its capture id.
    /// <summary>Gets the capture ids that follow the references: <paramref name="elementCaptureId"/>, then one per reference.</summary>
    /// <param name="elementCaptureId">The attached element's capture id, or <see langword="null"/> for none.</param>
    /// <param name="references">The references, or <see langword="null"/>.</param>
    /// <returns>A new array, or <see langword="null"/> when neither the element nor any reference is an element.</returns>
    internal static string?[]? CaptureIds(string? elementCaptureId, TwinReferences? references)
    {
        if (elementCaptureId is null && references is not { hasElement: true })
        {
            return null;
        }

        string?[] ids = new string?[1 + (references?.Count ?? 0)];
        ids[0] = elementCaptureId;
        references?.captures?.CopyTo(ids, 1);
        return ids;
    }

    private int Append(object reference, string? captureId)
    {
        references ??= [];
        captures ??= [];
        references.Add(reference);
        captures.Add(captureId);
        hasElement |= captureId is not null;
        return references.Count - 1;
    }
}
