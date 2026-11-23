using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// Base UI's CSPProvider (csp-provider/CSPProvider.tsx); the contract is shared-utilities.md §4.12 (UTL-DOM-32..36) and UTL §10's
// item "CspProvider in the port" (ADR 09 Amendments 2026-10-22: a family folder without CONTEXT.md, the DirectionProvider
// precedent). A single-part component (ADR 08 §1) with no state record, no data attributes and no attribute bag: Base UI's
// CSPProvider.State is empty. UTL-DOM-36: it renders no element, only the fixed cascade of its context (ADR 16 rule 9).
/// <summary>
/// Gives the components inside it a content security policy configuration: Base UI's <c>CSPProvider</c>. The components
/// that render an inline <c>style</c> element place <see cref="Nonce"/> on it, or render none under
/// <see cref="DisableStyleElements"/>. The nearest provider applies. The provider renders no element.
/// </summary>
public sealed class CspProvider : ComponentBase
{
    private readonly CspProviderContext context = new();
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    /// <summary>Gets or sets the nonce placed on the inline <c>style</c> and <c>script</c> elements the components render.</summary>
    /// <value>
    /// The nonce your content security policy allows, or <see langword="null"/> for none. The default is
    /// <see langword="null"/>.
    /// </value>
    [Parameter]
    public string? Nonce { get; set; }

    /// <summary>Gets or sets a value indicating whether the components render no inline <c>style</c> elements.</summary>
    /// <value>
    /// <see langword="true"/> to render none, so your own stylesheet provides the rules for the class names the components
    /// still apply; the default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool DisableStyleElements { get; set; }

    /// <summary>Gets or sets the content that reads the configuration.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The readers subscribed to the fixed cascade re-render now, in the batch that applies the new values.
        context.Set(Nonce, DisableStyleElements);

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
