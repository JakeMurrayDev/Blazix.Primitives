using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Iodine.Headless;

// TST §3.8 state object (toast/action/ToastAction.tsx:37-39); TST-STA-26.
/// <summary>Represents the state of a toast's action button, which the part exposes as data attributes.</summary>
/// <param name="Type">The toast's type, or <see langword="null"/>.</param>
[PartState]
public sealed partial record ToastActionState(string? Type);

// toast/action/ToastActionDataAttributes.ts:5 (TST-DOM-18).
/// <summary>Provides the data attributes a toast's action button renders.</summary>
public static class ToastActionDataAttributes
{
    /// <summary>The toast's type, when it has one.</summary>
    public const string Type = "data-type";
}

// Base UI's Toast.Action (toast/action/ToastAction.tsx); TST-STR-07, TST-API-16, TST-DOM-18, TST-DOM-19, TST-EVT-09,
// TST-PTR-27. Over ButtonBehavior with Native from NativeButton and no FocusableWhenDisabled (UBT §6.2's Toast.Action row).
// The attribute set is, in increasing precedence, the consumer's attributes, then the toast's ActionAttributes (handlers of
// one event chained, MergeProps), then the button mechanism's; the content is the toast's ActionContent, else the part's
// own. Without content, its own, the toast's or a substitute element's, the part renders nothing (RequiresContent, ADR 02
// Amendments 2026-11-18). The action does not close its toast.
/// <summary>
/// Represents a button that performs the toast's action. Base UI's <c>Toast.Action</c>; it renders a <c>button</c>, and
/// nothing when it has no content.
/// </summary>
/// <remarks>
/// The toast's <see cref="ToastObject{TData}.ActionAttributes"/> take precedence over the attributes written on the part,
/// and its <see cref="ToastObject{TData}.ActionContent"/> over the part's content.
/// </remarks>
public sealed class ToastAction : PartBase<ToastActionState>
{
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private ToastRootContext? root;
    private ButtonBehavior? button;
    private ToastRootSubscription? subscription;
    private IReadOnlyDictionary<string, object>? suppliedAttributes;
    private IReadOnlyDictionary<string, object>? mergedFrom;
    private IReadOnlyDictionary<string, object>? mergedWith;
    private IReadOnlyDictionary<string, object>? merged;
    private RenderFragment? suppliedContent;
    private RenderFragment? resolvedContent;
    private ToastActionState state = new(null);

    /// <summary>Initializes a new instance of the <see cref="ToastAction"/> class.</summary>
    public ToastAction()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", e => button!.HandleKeyDownAsync(e));
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<ToastAction> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [CascadingParameter]
    private ToastRootContext? RootContext { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native button.</summary>
    /// <value>
    /// <see langword="true"/> (the default) for a native <c>button</c>; <see langword="false"/> when a substitute renders
    /// another element, which then gets the button role and keyboard activation.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value><see langword="true"/> to disable the button; the default is <see langword="false"/>.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override ToastActionState State
    {
        get
        {
            if (!string.Equals(state.Type, root!.Type, StringComparison.Ordinal))
            {
                state = new ToastActionState(root.Type);
            }

            return state;
        }
    }

    // ADR 02 decision 1: under a substitute callback the button's twin finds the element by id.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => RenderFunction is not null;

    // ADR 02 Amendments 2026-11-18 (Toast DP11, settled by the orchestrator 2026-11-18): TST-DOM-19's renderable check, a
    // substitute element's own children included.
    /// <inheritdoc />
    protected override bool RequiresContent => true;

    // For the fixtures: completes once the button's handle is attached.
    internal Task TwinAttached => button?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = ToastRootContext.Require(RootContext);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        subscription = root.Subscribe(ToastRootAspects.Type | ToastRootAspects.Action, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // A parameter set that does not supply them leaves this part's merged attributes and resolved content in place.
        suppliedAttributes = merged is not null && ReferenceEquals(AdditionalAttributes, merged) ? suppliedAttributes : AdditionalAttributes;
        suppliedContent = resolvedContent is not null && ReferenceEquals(ChildContent, resolvedContent) ? suppliedContent : ChildContent;
        button!.Native = NativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = Disabled;
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        subscription!.Rendered();
        // TST-DOM-19: the toast's attributes after the consumer's, merged once per change of either.
        IReadOnlyDictionary<string, object>? toastAttributes = root!.ActionAttributes;
        if (toastAttributes is null)
        {
            merged = suppliedAttributes;
        }
        else if (merged is null || !ReferenceEquals(mergedFrom, suppliedAttributes) || !ReferenceEquals(mergedWith, toastAttributes))
        {
            mergedFrom = suppliedAttributes;
            mergedWith = toastAttributes;
            merged = MergeProps.MergeDictionaries(suppliedAttributes, toastAttributes);
        }

        AdditionalAttributes = merged;
        button!.SetConsumerAttributes(merged);
        resolvedContent = root.ActionContent ?? suppliedContent;
        ChildContent = resolvedContent;
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender)
    {
        button!.Disabled = Disabled;
        return button.AfterRenderAsync(Element is not null || RenderFunction is not null, Element, Id, composite: false);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
            button?.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        button!.Disabled = Disabled;
        button.AddAttributes(attributes, Id, composite: false);
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) => button!.ForwardsConsumerHandler(eventName);
}
