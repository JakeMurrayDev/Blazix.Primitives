using System.Globalization;
using System.Text;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The base of the Select tests (M5 track 9), written as Razor test classes so each Base UI tree reads as markup: the
/// library's services, every module loose, and the attaches of the <c>interactions</c>, <c>composite</c>, <c>select</c>
/// and <c>form</c> modules recorded so a test drives their twins as the modules report on Server (ADR 06 §1). A press on
/// the trigger is the <c>interactions</c> module's report; the order of the items is the <c>composite</c> module's report
/// on the popup, which <see cref="OpenAsync"/> sends after the popup rendered, as the module does once its items attached;
/// an item click is the <c>select</c> module's activation report from the positioner; the exit is the
/// <c>transitions</c> module's report, stood in for by <see cref="CompleteTransitionAsync{TValue}"/>.
/// </summary>
public abstract class SelectTestBase : BunitContext
{
    public const string InteractionsPath = "./_content/Blazix.Primitives/js/interactions.js";

    public const string CompositePath = "./_content/Blazix.Primitives/js/composite.js";

    public const string SelectPath = "./_content/Blazix.Primitives/js/select.js";

    public const string FormPath = "./_content/Blazix.Primitives/js/form.js";

    public const string PositioningPath = "./_content/Blazix.Primitives/js/positioning.js";

    private const string ClickTemplate = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"{0}","detail":{1},"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    private const string KeyTemplate = """
        {"kind":"KeyboardEvent","type":"keydown","key":"{0}","code":"{0}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    protected SelectTestBase()
    {
        Services.AddBlazixPrimitives();
        JSInterop.Mode = JSRuntimeMode.Loose;
        Interactions = JSInterop.SetupModule(InteractionsPath);
        InteractionsHandle = SetupHandle(Interactions);
        Composite = JSInterop.SetupModule(CompositePath);
        CompositeHandle = SetupHandle(Composite);
        Select = JSInterop.SetupModule(SelectPath);
        SelectHandle = SetupHandle(Select);
        Form = JSInterop.SetupModule(FormPath);
        FormHandle = SetupHandle(Form);
        Positioning = JSInterop.SetupModule(PositioningPath);
        PositioningHandle = SetupHandle(Positioning);
    }

    public BunitJSModuleInterop Interactions { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    public BunitJSModuleInterop Composite { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    public BunitJSModuleInterop Select { get; }

    public BunitJSModuleInterop SelectHandle { get; }

    public BunitJSModuleInterop Form { get; }

    public BunitJSModuleInterop FormHandle { get; }

    public BunitJSModuleInterop Positioning { get; }

    public BunitJSModuleInterop PositioningHandle { get; }

    /// <summary>Gets the snapshot JSON of a click, as the module snapshots it.</summary>
    public static string Click(string pointerType = "mouse", int detail = 1) =>
        ClickTemplate.Replace("{0}", pointerType, StringComparison.Ordinal).Replace("{1}", detail.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Gets the snapshot JSON of a keydown.</summary>
    public static string Key(string key) => KeyTemplate.Replace("{0}", key, StringComparison.Ordinal);

    /// <summary>Renders <paramref name="fragment"/> inside a <see cref="SelectHost"/>, which a test renders again with <see cref="RefreshAsync"/>.</summary>
    public IRenderedComponent<SelectHost> RenderSelect(RenderFragment fragment) => Render<SelectHost>(p => p.AddChildContent(fragment));

    /// <summary>Renders the host again, so the fragment reads the test's variables anew.</summary>
    public static Task RefreshAsync(IRenderedComponent<SelectHost> cut) => cut.Instance.RefreshAsync();

    /// <summary>Gets the trigger element (the combobox).</summary>
    public static IElement Trigger(IRenderedComponent<SelectHost> cut) => cut.Find("[role=\"combobox\"]");

    /// <summary>Gets the root's store.</summary>
    internal static SelectStore<TValue> StoreOf<TValue>(IRenderedComponent<SelectHost> cut) => cut.FindComponent<SelectRoot<TValue>>().Instance.Store;

    /// <summary>Presses the trigger as the module reports a press on Server, then reports the items' order once the popup rendered.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="open">The requested state, the module's toggle.</param>
    /// <param name="pointerType">The interaction type.</param>
    public async Task PressAsync(IRenderedComponent<SelectHost> cut, bool open = true, string pointerType = "mouse")
    {
        string triggerId = Trigger(cut).Id!;
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"trigger-press","triggerId":"{{triggerId}}","interactionType":"{{pointerType}}","event":{{Click(pointerType)}}}""";
        InteractionsTwin twin = InteractionsTwinOf("\"triggerId\":\"" + triggerId + "\"");
        await cut.InvokeAsync(() => twin.OnPress(json));
        if (open)
        {
            await PublishOrderAsync(cut);
        }
    }

    /// <summary>Opens the popup with a mouse press on the trigger and reports the items' order.</summary>
    public Task OpenAsync(IRenderedComponent<SelectHost> cut) => PressAsync(cut, open: true);

    /// <summary>Reports the order of the rendered options, as the popup's <c>composite</c> handle does after its items attached.</summary>
    public async Task PublishOrderAsync(IRenderedComponent<SelectHost> cut)
    {
        string? json = null;
        CompositeTwin? twin = null;
        await cut.InvokeAsync(() =>
        {
            IReadOnlyList<string> ids = [.. cut.FindAll("[role=\"option\"]").Select(option => option.Id!)];
            json = IdsJson(ids);
            twin = PopupCompositeTwin();
        });
        if (twin is not null)
        {
            await cut.InvokeAsync(() => twin.OnItemsChange(json!));
        }
    }

    /// <summary>Clicks an option as the <c>select</c> module reports an activation from the positioner on Server.</summary>
    /// <param name="cut">The rendered host.</param>
    /// <param name="option">The option element.</param>
    public async Task ClickOptionAsync(IRenderedComponent<SelectHost> cut, IElement option, string pointerType = "mouse")
    {
        string json = $$"""{"itemId":"{{option.Id}}","event":{{Click(pointerType)}}}""";
        SelectTwin twin = SelectTwinOf("\"part\":\"positioner\"") ?? SelectTwinOf(null)!;
        await cut.InvokeAsync(() => twin.OnActivate(json));
    }

    /// <summary>Clicks the option whose text is <paramref name="text"/>.</summary>
    public Task ClickOptionAsync(IRenderedComponent<SelectHost> cut, string text) =>
        ClickOptionAsync(cut, cut.FindAll("[role=\"option\"]").First(option => option.TextContent.Trim() == text));

    /// <summary>Requests a change through the session, as the module reports Escape, an outside press or a focus-out.</summary>
    public async Task SessionRequestAsync(IRenderedComponent<SelectHost> cut, bool open, string reason, string? eventJson = null)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson ?? Click()}},"triggerId":null}""";
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null).Arguments[2]!).Value;
        await cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Reports a typeahead match while the list is closed, as the trigger's <c>composite</c> handle does.</summary>
    public async Task MatchAsync(IRenderedComponent<SelectHost> cut, int index, string key)
    {
        string json = $$"""{"index":{{index.ToString(CultureInfo.InvariantCulture)}},"event":{{Key(key)}}}""";
        CompositeTwin twin = CompositeTwinOf("\"reference\":")!;
        await cut.InvokeAsync(() => twin.OnListMatch(json));
    }

    /// <summary>Reports the list's highlight, as the <c>composite</c> module does after a key or a pointer moved it.</summary>
    public async Task HighlightAsync(IRenderedComponent<SelectHost> cut, int? index)
    {
        string? itemId = index is { } i ? cut.FindAll("[role=\"option\"]")[i].Id : null;
        string indexJson = index?.ToString(CultureInfo.InvariantCulture) ?? "null";
        string itemJson = itemId is null ? "null" : "\"" + itemId + "\"";
        CompositeTwin twin = PopupCompositeTwin()!;
        int revision = 0;
        foreach (JSRuntimeInvocation update in CompositeHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string options && options.Contains("\"highlight\":", StringComparison.Ordinal))
            {
                revision++;
            }
        }

        string json = $$"""{"index":{{indexJson}},"itemId":{{itemJson}},"event":{{Key("ArrowDown")}},"revision":{{revision.ToString(CultureInfo.InvariantCulture)}}}""";
        await cut.InvokeAsync(() => twin.OnListHighlight(json));
    }

    /// <summary>Reports the positioner's placement, as the <c>positioning</c> module does after a pass.</summary>
    public async Task PlaceAsync(IRenderedComponent<SelectHost> cut, string side = "bottom", string align = "center")
    {
        JSRuntimeInvocation attach = Positioning.Invocations["attach"][^1];
        PositioningTwin twin = ((DotNetObjectReference<PositioningTwin>)attach.Arguments[2]!).Value;
        string json = $$"""{"side":"{{side}}","align":"{{align}}","anchorHidden":false,"uncentered":false}""";
        await cut.InvokeAsync(() => twin.OnPlacementChange(json));
    }

    /// <summary>Stands in for the popup's <c>transitions</c> report that its exit (or enter) finished.</summary>
    public static Task CompleteTransitionAsync<TValue>(IRenderedComponent<SelectHost> cut, bool opened = false)
    {
        SelectRoot<TValue> root = cut.FindComponent<SelectRoot<TValue>>().Instance;
        return cut.InvokeAsync(() => root.Store.CompleteTransitionAsync(opened));
    }

    /// <summary>Closes the popup with an outside press and completes the exit.</summary>
    public async Task CloseAsync<TValue>(IRenderedComponent<SelectHost> cut)
    {
        await SessionRequestAsync(cut, open: false, "outside-press");
        await CompleteTransitionAsync<TValue>(cut);
    }

    /// <summary>Gets the twin of the <c>select</c> handle whose options carry <paramref name="marker"/>, or the last one.</summary>
    internal SelectTwin? SelectTwinOf(string? marker)
    {
        JSRuntimeInvocation[] attaches = [.. Select.Invocations["attach"].Where(invocation => marker is null || (invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal)))];
        return attaches.Length == 0 ? null : ((DotNetObjectReference<SelectTwin>)attaches[^1].Arguments[2]!).Value;
    }

    /// <summary>Gets the twin of the <c>interactions</c> handle whose options carry <paramref name="marker"/>.</summary>
    internal InteractionsTwin InteractionsTwinOf(string marker)
    {
        JSRuntimeInvocation attach = Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        return ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
    }

    /// <summary>Gets the twin of the <c>composite</c> handle whose options carry <paramref name="marker"/>, or <see langword="null"/>.</summary>
    internal CompositeTwin? CompositeTwinOf(string marker)
    {
        JSRuntimeInvocation[] attaches = [.. Composite.Invocations["attach"].Where(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal))];
        return attaches.Length == 0 ? null : ((DotNetObjectReference<CompositeTwin>)attaches[^1].Arguments[2]!).Value;
    }

    /// <summary>Gets the twin of the popup's <c>composite</c> handle, the one that reports its items.</summary>
    internal CompositeTwin? PopupCompositeTwin() => CompositeTwinOf("\"reportItems\":true");

    /// <summary>Gets the options JSON of the <c>select</c> handle whose attach carries <paramref name="marker"/>: its attach's, or its latest update's.</summary>
    public string? LastSelectOptions(string marker)
    {
        string? options = Select.Invocations["attach"].Select(invocation => invocation.Arguments[1] as string).LastOrDefault(json => json is not null && json.Contains(marker, StringComparison.Ordinal));
        foreach (JSRuntimeInvocation update in SelectHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options;
    }

    private static string IdsJson(IReadOnlyList<string> ids)
    {
        StringBuilder builder = new("{\"ids\":[");
        for (int i = 0; i < ids.Count; i++)
        {
            builder.Append(i == 0 ? "\"" : ",\"").Append(ids[i]).Append('"');
        }

        return builder.Append("]}").ToString();
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
