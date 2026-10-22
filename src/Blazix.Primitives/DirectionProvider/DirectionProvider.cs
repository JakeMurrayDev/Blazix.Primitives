using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's DirectionProvider (direction-provider/DirectionProvider.tsx); the contract is shared-utilities.md §3.1 and
// §4.15.1 (ADR 09 Amendments 2026-10-22: a family folder without CONTEXT.md). A single-part component (ADR 08 §1) with no
// state record, no data attributes and no attribute bag: Base UI's DirectionProvider.State is empty. UTL-DOM-36: it renders
// no element, only the fixed cascade of its context (ADR 16 rule 9). UTL-I18N-02: Direction defaults to Ltr.
/// <summary>
/// Gives the components inside it a reading direction: Base UI's <c>DirectionProvider</c>. Under
/// <see cref="TextDirection.Rtl"/>, the left and right arrow keys of a roving group such as a radio group swap their
/// meaning. The provider renders no element and does not set the <c>dir</c> attribute; set <c>dir</c> or the CSS
/// <c>direction</c> on an element yourself.
/// </summary>
public sealed class DirectionProvider : ComponentBase
{
    private readonly DirectionProviderContext context = new();
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    /// <summary>Gets or sets the reading direction of the text.</summary>
    /// <value>The direction the components inside read. The default is <see cref="TextDirection.Ltr"/>.</value>
    [Parameter]
    public TextDirection Direction { get; set; } = TextDirection.Ltr;

    /// <summary>Gets or sets the content that reads the direction.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The readers subscribed to the fixed cascade re-render now, in the batch that applies the new direction.
        context.SetDirection(Direction);

        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, cascade);
    }
}
