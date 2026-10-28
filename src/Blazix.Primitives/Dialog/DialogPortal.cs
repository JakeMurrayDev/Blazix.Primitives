using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Dialog.Portal (dialog/portal/DialogPortal.tsx); the contract is docs/components/dialog.md (§3.3, §5.2). A thin
// part over DialogPortalBehavior, which AlertDialogPortal shares (ADR 08 Amendments 2026-10-27). Its host is the portal node,
// wrapped by the pipeline's `display: contents` host wrapper (ADR 02 Amendments 2026-10-27).
/// <summary>
/// Moves the dialog's popup to another part of the document, Base UI's <c>Dialog.Portal</c>. Renders a <c>div</c> that is
/// appended to the <c>body</c> by default.
/// </summary>
/// <remarks>
/// The viewport and the popup must be placed inside it. Its <c>class</c>, <c>style</c> and other attributes apply to the
/// moved element.
/// </remarks>
public sealed class DialogPortal : PartBase<EmptyState>
{
    private DialogPortalBehavior? behavior;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    // DLG-API-17
    /// <summary>Gets or sets a value indicating whether the portal's content stays in the document while the dialog is closed.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool KeepMounted { get; set; }

    // DLG-API-18
    /// <summary>Gets or sets the element the portal's element is appended to.</summary>
    /// <value>
    /// <see langword="null"/>, the default, for the enclosing portal's element, else the <c>body</c>; an element, a node a
    /// script holds such as a shadow root, or a selector; <see cref="PortalContainerValue.Pending"/> to render nothing until
    /// the container is known.
    /// </value>
    [Parameter]
    public PortalContainerValue? Container { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override EmptyState State => EmptyState.Instance;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override string? HostWrapperTagName => "div";

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Dialog");
        }

        behavior = new DialogPortalBehavior(JSRuntime, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ChildContent = behavior!.Apply(EnclosingPortal, Id, KeepMounted, ChildContent);
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.Renders(KeepMounted, Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Container);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && behavior is not null)
        {
            _ = behavior.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors() => behavior!.Subscribe(Context!);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set("data-base-ui-portal", string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => DialogPortalBehavior.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.BuildAfterElement(builder);
}
