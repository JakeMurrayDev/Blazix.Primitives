using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

// A descendant that adds its toasts through the cascaded manager from the parameter set that activates it: the port's
// form of ToastProvider.test.tsx's AddToastInLayoutEffect (a descendant's post-commit work).
public sealed class ToastAdder : ComponentBase
{
    private bool added;

    [CascadingParameter]
    public ToastManager<NoPayload>? Manager { get; set; }

    [Parameter]
    public bool Active { get; set; }

    [Parameter]
    public IReadOnlyList<ToastObject>? Toasts { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Active && !added && Toasts is not null)
        {
            added = true;
            foreach (ToastObject toast in Toasts)
            {
                Manager!.Add(toast);
            }
        }
    }
}

// ToastProvider.test.tsx's ObserveToastsInLayoutEffect: records the store's toasts, with their limited flags, in the
// parameter set that activates it, after the adder before it in the tree.
public sealed class ToastObserver : ComponentBase
{
    [CascadingParameter]
    internal ToastProviderContext<NoPayload>? Provider { get; set; }

    [Parameter]
    public bool Active { get; set; }

    [Parameter]
    public List<IReadOnlyList<(string Id, bool Limited)>>? Observed { get; set; }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (Active)
        {
            Observed?.Add([.. Provider!.Store.State.Toasts.Select(toast => (toast.Id, toast.Limited))]);
        }
    }
}

// The forms of useToastLabelPart.test.tsx's part.
public enum ToastLabelForm
{
    Plain,
    RenderProp,
    RenderFunction,
    ChildlessRenderProp,
    EmptyRenderFunction,
    OldAndNew,
}
