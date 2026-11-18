using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Probe.Parts;

// ADR 08 Amendments 2026-11-18 (the Toast line; decisions 17 (a) and 18 (a)): ToastObject<TData> and ToastManager<TData>
// with the non-generic defaults over NoPayload; ToastRoot<TData> infers TData through Toast; the other parts are
// non-generic; the re-render surface is ToastViewport.ChildContent : RenderFragment<IReadOnlyList<ToastObject>> and the
// manager's Changed event. The non-generic defaults derive from the generic types over NoPayload.
public readonly record struct NoPayload;

public record ToastObject<TData>(string Id)
{
    public string? Title { get; init; }

    public TData? Data { get; init; }
}

public sealed record ToastObject(string Id) : ToastObject<NoPayload>(Id);

public class ToastManager<TData>
{
    private readonly List<ToastObject<TData>> toasts = [];

    public event Action? Changed;

    public IReadOnlyList<ToastObject<TData>> Toasts => toasts;

    public string Add(ToastObject<TData> toast)
    {
        toasts.Add(toast);
        Changed?.Invoke();
        return toast.Id;
    }
}

public sealed class ToastManager : ToastManager<NoPayload>;

public sealed class ToastRoot<TData> : ComponentBase
{
    [Parameter, EditorRequired]
    public ToastObject<TData> Toast { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, $"[ToastRoot<{TypeName.Of<TData>()}> {Toast.GetType().Name} {Toast.Id} Data={Toast.Data}: ");
        builder.AddContent(1, ChildContent);
        builder.AddContent(2, "]");
    }
}

// The viewport's list stands in for what the Provider's manager supplies.
public sealed class ToastViewport : ComponentBase
{
    [Parameter]
    public IReadOnlyList<ToastObject> Toasts { get; set; } = [];

    [Parameter]
    public RenderFragment<IReadOnlyList<ToastObject>>? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.AddContent(0, "[ToastViewport ");
        builder.AddContent(1, ChildContent?.Invoke(Toasts));
        builder.AddContent(2, "]");
    }
}

public sealed class ToastTitle : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "[ToastTitle]");
}

public sealed class ToastDescription : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "[ToastDescription]");
}

public sealed class ToastClose : ComponentBase
{
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, "[ToastClose]");
}

public sealed record MyData(int Count)
{
    public override string ToString() => $"MyData({Count})";
}
