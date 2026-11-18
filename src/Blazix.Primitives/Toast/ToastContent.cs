using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// TST §3.5 state object (toast/content/ToastContent.tsx:44-49); TST-STA-26.
/// <summary>Represents the state of a toast's content, which the part exposes as data attributes.</summary>
/// <param name="Expanded">A value indicating whether the stack of toasts is expanded.</param>
/// <param name="Behind">A value indicating whether the toast sits behind the frontmost toast.</param>
[PartState]
public sealed partial record ToastContentState(bool Expanded, bool Behind);

// toast/content/ToastContentDataAttributes.ts:5,10 (TST-DOM-15).
/// <summary>Provides the data attributes a toast's content renders.</summary>
public static class ToastContentDataAttributes
{
    /// <summary>Present while the stack of toasts is expanded.</summary>
    public const string Expanded = "data-expanded";

    /// <summary>Present while the toast sits behind the frontmost toast.</summary>
    public const string Behind = "data-behind";
}

// Base UI's Toast.Content (toast/content/ToastContent.tsx); TST-STR-04, TST-DOM-15. Its re-measurement on every size change
// and DOM mutation of its subtree (TST-ANI-03) is the `toast` module's content role (M6 track 9b).
/// <summary>Represents the container of a toast's contents. Base UI's <c>Toast.Content</c>; it renders a <c>div</c>.</summary>
public sealed class ToastContent : PartBase<ToastContentState>
{
    private ToastRootContext? root;
    private ToastRootSubscription? subscription;
    private ToastContentState state = new(false, false);

    [CascadingParameter]
    private ToastRootContext? RootContext { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ToastContentState State
    {
        get
        {
            if (state.Expanded != root!.Expanded || state.Behind != root.Behind)
            {
                state = new ToastContentState(root.Expanded, root.Behind);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        root = ToastRootContext.Require(RootContext);
        subscription = root.Subscribe(ToastRootAspects.Expanded | ToastRootAspects.Behind, StateHasChanged);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        subscription!.Rendered();
        base.BuildRenderTree(builder);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            subscription?.Dispose();
            subscription = null;
        }

        base.Dispose(disposing);
    }
}
