using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// DRW §3.15: Dialog.Close's state object (DLG §3.9, dialog/close/DialogClose.tsx:37) under its Drawer name.
/// <summary>Represents the state of a drawer's close button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button is disabled; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record DrawerCloseState(bool Disabled);

// DRW-STR-02: Base UI re-exports DialogCloseDataAttributes as DrawerCloseDataAttributes (DLG-DOM-24).
/// <summary>Provides the names of the data attributes a drawer's close button renders.</summary>
public static class DrawerCloseDataAttributes
{
    /// <summary>Represents the attribute present while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Drawer.Close, the Dialog.Close component itself (drawer/close/DrawerClose.tsx:12); the contract is
// docs/components/drawer.md (§3.15; DRW-STR-03 over dialog.md §3.9, §5.6). A sealed part over
// DialogCloseBehavior, which DialogClose shares (ADR 08 Amendments 2026-10-27; decision 20 (a) of the M6 plan, chosen by the user 2026-11-18).
/// <summary>Represents a button that closes the drawer, Base UI's <c>Drawer.Close</c>, rendered as a <c>button</c>.</summary>
public sealed class DrawerClose : PartBase<DrawerCloseState>
{
    private readonly EventCallback onClick;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private DialogCloseBehavior? behavior;
    private DrawerCloseState state = new(false);

    /// <summary>Initializes a new instance of the <see cref="DrawerClose"/> class.</summary>
    public DrawerClose()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => behavior!.HandleClickAsync(e, Disabled));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => behavior!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<DrawerClose> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private IDialogRootContext? Context { get; set; }

    // DLG-API-22
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>The default is <see langword="true"/>; set it to <see langword="false"/> when <c>Render</c> substitutes another element.</value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    // DLG-API-23
    /// <summary>Gets or sets a value indicating whether the button is disabled.</summary>
    /// <value>The default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override DrawerCloseState State
    {
        get
        {
            if (state.Disabled != Disabled)
            {
                state = new DrawerCloseState(Disabled);
            }

            return state;
        }
    }

    // ADR 02 decision 1: under a substitute callback the button's twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // For the fixtures: completes once the button's handle is attached.
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        if (Context is null)
        {
            throw DialogParts.MissingRoot("Drawer");
        }

        behavior = new DialogCloseBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet() => behavior!.Apply(Context!, NativeButton, Disabled, AdditionalAttributes);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.OnAfterRenderAsync(Enabled, Element, Id, Disabled);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            behavior?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => behavior!.AddAttributes(attributes, Id, Disabled, onClick);

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => behavior!.ForwardsConsumerHandler(eventName);
}
