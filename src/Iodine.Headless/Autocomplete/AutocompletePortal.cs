using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// Base UI's Combobox.Portal (combobox/portal/AutocompletePortal.tsx; CMB §3.12): a sealed thin part over ComboboxPortalBehavior,
// the lite portal's in-place wrapper (ADR 04 Amendments 2026-10-27), rendered iff mounted, kept mounted or force-mounted.
/// <summary>Represents the portal that moves an autocomplete's popup to the end of the document, Base UI's <c>Autocomplete.Portal</c>.</summary>
public sealed class AutocompletePortal : PartBase<EmptyState>
{
    private ComboboxPortalBehavior? behavior;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<AutocompletePortal> Logger { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ComboboxStore? Root { get; set; }

    [CascadingParameter]
    private PopupPortalContext? EnclosingPortal { get; set; }

    // CMB-API-48
    /// <summary>Gets or sets a value indicating whether the popup stays in the document while closed.</summary>
    [Parameter]
    public bool KeepMounted { get; set; }

    // CMB-API-49
    /// <summary>Gets or sets the element the popup is moved into.</summary>
    /// <value><see langword="null"/>, the default, for the document's body.</value>
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
    public override Task SetParametersAsync(ParameterView parameters)
    {
        // The part replaces ChildContent with its own cascade, so a parameter set without content must not keep it.
        ChildContent = null;
        return base.SetParametersAsync(parameters);
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new ComboboxPortalBehavior(new ComboboxPartServices("Autocomplete", JSRuntime, Logger, IdGenerator, StateHasChanged, InvokeAsync, DispatchExceptionAsync));
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => ChildContent = behavior!.Apply(KeepMounted, EnclosingPortal, Id, ChildContent);

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        Enabled = behavior!.Shown(KeepMounted, Container);
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync(Enabled ? Element : null, Container);

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
    protected override void SubscribeSelectors() => behavior!.Subscribe(Root, SubscribeTo);

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => attributes.Set(PopupMarkerAttributes.Portal, string.Empty);

    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes) => behavior!.Portal.FinishAttributes(attributes);

    /// <inheritdoc />
    protected override void AddHostWrapperAttributes(AttributeList attributes) => PopupPortalBehavior.AddWrapperAttributes(attributes);

    /// <inheritdoc />
    protected override void BuildBeforeElement(RenderTreeBuilder builder) => behavior!.Portal.BuildBeforeElement(builder);

    /// <inheritdoc />
    protected override void BuildAfterElement(RenderTreeBuilder builder) => behavior!.Portal.BuildAfterElement(builder);
}
