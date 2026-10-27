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
        return Append(element);
#pragma warning restore BLZX2005
    }

    /// <summary>Adds an object a script holds, such as a closed shadow root.</summary>
    /// <param name="reference">The reference, revived by Blazor as the object itself in the module.</param>
    /// <returns>The index the option that names the object carries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="reference"/> is <see langword="null"/>.</exception>
    public int Add(IJSObjectReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return Append(reference);
    }

    /// <summary>Gets the interop argument that follows the options string.</summary>
    /// <returns>A new array of the references in the order they were added, or <see langword="null"/> when there are none.</returns>
    internal object[]? ToArgument() => references is { Count: > 0 } ? [.. references] : null;

    private int Append(object reference)
    {
        references ??= [];
        references.Add(reference);
        return references.Count - 1;
    }
}
