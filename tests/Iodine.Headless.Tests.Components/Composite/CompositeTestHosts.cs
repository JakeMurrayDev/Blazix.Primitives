using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A bUnit context with the <c>composite</c> module mocked: the attach, update and dispose calls are recorded and the
/// twin of each attach is reachable, so a test drives its <c>[JSInvokable]</c> callbacks as the module would.
/// </summary>
internal sealed class CompositeTestContext : IDisposable
{
    public const string ModulePath = "./_content/Blazix.Primitives/js/composite.js";

    public CompositeTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Module = Context.JSInterop.SetupModule(ModulePath);
        Handle = Module.SetupModule("attach", _ => true);
        Handle.SetupVoid("update", _ => true).SetVoidResult();
        Handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Module { get; }

    public BunitJSModuleInterop Handle { get; }

    public IReadOnlyList<JSRuntimeInvocation> Attaches => Module.Invocations["attach"];

    public IReadOnlyList<string> UpdateOptions => [.. Handle.Invocations["update"].Select(invocation => (string)invocation.Arguments[0]!)];

    public int Disposes => Handle.Invocations["dispose"].Count;

    /// <summary>Gets the twin of the last attach, which the module would call back.</summary>
    public CompositeTwin Twin => ((DotNetObjectReference<CompositeTwin>)Attaches[^1].Arguments[2]!).Value;

    public static JsonElement Parse(string optionsJson) => JsonDocument.Parse(optionsJson).RootElement;

    /// <summary>Gets the item ids an options JSON carries, in the order sent.</summary>
    public static string[] ItemIds(string optionsJson) => [.. Parse(optionsJson).GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()!)];

    /// <summary>Gets the ids of the rendered item elements, in document order.</summary>
    public static string[] RenderedItemIds<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
        => [.. cut.FindAll("[data-item]").Select(element => element.Id!)];

    public void Dispose() => Context.Dispose();
}

/// <summary>Builds the child content of a test root: one <see cref="TestCompositeItem"/> per label.</summary>
internal static class CompositeItems
{
    public static RenderFragment Of(params string[] labels) => builder =>
    {
        foreach (string label in labels)
        {
            builder.OpenComponent<TestCompositeItem>(0);
            builder.SetKey(label);
            builder.AddAttribute(1, "data-item", label);
            builder.AddAttribute(2, "ChildContent", (RenderFragment)(content => content.AddContent(0, label)));
            builder.CloseComponent();
        }
    };
}

/// <summary>An item behind its own state, which adds or removes it without the root rendering (a nested state update).</summary>
internal sealed class HideableItem : ComponentBase
{
    [Parameter]
    public string Label { get; set; } = "";

    [Parameter]
    public bool InitiallyHidden { get; set; }

    [Parameter]
    public string Tag { get; set; } = "div";

    public bool Hidden { get; private set; }

    public void SetHidden(bool hidden)
    {
        Hidden = hidden;
        StateHasChanged();
    }

    protected override void OnInitialized() => Hidden = InitiallyHidden;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Hidden)
        {
            return;
        }

        builder.OpenComponent<TestCompositeItem>(0);
        builder.AddAttribute(1, "data-item", Label);
        builder.AddAttribute(2, nameof(TestCompositeItem.Tag), Tag);
        builder.CloseComponent();
    }
}

/// <summary>Renders the direction it reads from the nearest provider, as Base UI's <c>DirectionProbe</c> with <c>useDirection</c>.</summary>
internal sealed class DirectionProbe : ComponentBase, IDisposable
{
    private DirectionProviderContext? subscribed;

    [CascadingParameter]
    private DirectionProviderContext? DirectionContext { get; set; }

    public void Dispose() => subscribed?.RemoveListener(StateHasChanged);

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(subscribed, DirectionContext))
        {
            subscribed?.RemoveListener(StateHasChanged);
            subscribed = DirectionContext;
            subscribed?.AddListener(StateHasChanged);
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "data-testid", "direction");
        builder.AddContent(2, CompositeLiterals.ToLiteral(DirectionProviderContext.Read(DirectionContext)));
        builder.CloseElement();
    }
}
