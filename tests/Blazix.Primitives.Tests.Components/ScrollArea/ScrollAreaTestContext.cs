using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the ScrollArea tests: the library's services and bUnit's JS interop mock standing in for the
/// <c>scroll-area</c> module, one handle per part. A test drives the root's twin as the module would (ADR 06 §1):
/// <see cref="OverflowAsync"/> is the measurement's report, <see cref="ScrollingAsync"/> and <see cref="HoveringAsync"/> the
/// flags' reports.
/// </summary>
public sealed class ScrollAreaTestContext : IDisposable
{
    public const string ModulePath = "./_content/Blazix.Primitives/js/scroll-area.js";

    public ScrollAreaTestContext()
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

    /// <summary>The options JSON of every attach, in order.</summary>
    public IReadOnlyList<string> Attaches => [.. Module.Invocations["attach"].Select(i => (string)i.Arguments[1]!)];

    /// <summary>The options JSON of every update, in order, whatever the part.</summary>
    public IReadOnlyList<string> Updates => [.. Handle.Invocations["update"].Select(i => (string)i.Arguments[0]!)];

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The options of every attach of a part, in order.</summary>
    public IReadOnlyList<JsonElement> AttachesOf(string part) =>
        [.. Attaches.Select(json => JsonDocument.Parse(json).RootElement).Where(o => o.GetProperty("part").GetString() == part)];

    /// <summary>The options of every update of a part, in order.</summary>
    public IReadOnlyList<JsonElement> UpdatesOf(string part) =>
        [.. Updates.Select(json => JsonDocument.Parse(json).RootElement).Where(o => o.GetProperty("part").GetString() == part)];

    /// <summary>The options a part sent last, by its update or else its attach.</summary>
    public JsonElement LastOptionsOf(string part) => UpdatesOf(part) is { Count: > 0 } updates ? updates[^1] : AttachesOf(part)[^1];

    /// <summary>The root's twin, which receives the reports.</summary>
    internal ScrollAreaTwin RootTwin()
    {
        for (int i = 0; i < Attaches.Count; i++)
        {
            if (JsonDocument.Parse(Attaches[i]).RootElement.GetProperty("part").GetString() == ScrollAreaTwin.RootPart)
            {
                return ((DotNetObjectReference<ScrollAreaTwin>)Module.Invocations["attach"][i].Arguments[2]!).Value;
            }
        }

        throw new InvalidOperationException("No root attached.");
    }

    /// <summary>Reports a measurement's overflow state, as the module does once per change.</summary>
    internal Task OverflowAsync<TComponent>(
        IRenderedComponent<TComponent> cut,
        bool hasX = false,
        bool hasY = false,
        bool xStart = false,
        bool xEnd = false,
        bool yStart = false,
        bool yEnd = false,
        bool measured = true)
        where TComponent : IComponent
    {
        string json = JsonSerializer.Serialize(new { hasOverflowX = hasX, hasOverflowY = hasY, xStart, xEnd, yStart, yEnd, measured });
        ScrollAreaTwin twin = RootTwin();
        return cut.InvokeAsync(() => twin.OnOverflow(json));
    }

    /// <summary>Reports the scrolling flags, as the module does once per change.</summary>
    internal Task ScrollingAsync<TComponent>(IRenderedComponent<TComponent> cut, bool x, bool y)
        where TComponent : IComponent
    {
        string json = JsonSerializer.Serialize(new { x, y });
        ScrollAreaTwin twin = RootTwin();
        return cut.InvokeAsync(() => twin.OnScrolling(json));
    }

    /// <summary>Reports the hovering flag, as the module does once per change.</summary>
    internal Task HoveringAsync<TComponent>(IRenderedComponent<TComponent> cut, bool hovering)
        where TComponent : IComponent
    {
        string json = JsonSerializer.Serialize(new { hovering });
        ScrollAreaTwin twin = RootTwin();
        return cut.InvokeAsync(() => twin.OnHovering(json));
    }
}
