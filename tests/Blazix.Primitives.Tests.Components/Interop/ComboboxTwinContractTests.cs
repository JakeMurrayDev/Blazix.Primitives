using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>combobox</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-11-03 (4)): bUnit's JS interop
/// mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive <see cref="ComboboxTwin"/>'s
/// <c>[JSInvokable]</c> callbacks and its <c>clear</c> and <c>remove</c> decision points.
/// </summary>
public sealed class ComboboxTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/combobox.js";

    private const string KeyJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-3","tagName":"INPUT"},"defaultPrevented":false,"timeStamp":2.5}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMB-EDGE-07")]
    public async Task Attach_imports_the_combobox_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using ComboboxTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ComboboxOptions> attached = await twin.AttachAsync("base-ui-1", new ComboboxOptions());

        // CMB-EDGE-07 restated (twin attach idempotency): a repeated attach of the same element is a no-op.
        Assert.Same(attached, await twin.AttachAsync("base-ui-1", new ComboboxOptions { Open = true }));
        await attached.UpdateAsync(new ComboboxOptions
        {
            Part = ComboboxTwin.InputPart,
            InsidePopup = true,
            Inline = true,
            Disabled = true,
            ReadOnly = true,
            Multiple = true,
            HasValue = true,
            Virtualized = true,
            ChipsId = "c",
            ListId = "l",
            PopupId = "p",
            Value = "v",
            ValueRevision = 3,
            Open = true,
            Mounted = true,
            Rtl = true,
            InputId = "i",
            TouchOpened = true,
            ChipIds = ["a", "b"],
            Index = 2,
            DismissId = "d",
            SubmitRevision = 4,
            SubmitFormElement = 0,
            DecidesKeyDown = true,
            DecidesPress = true,
            FocusOnAttach = true,
        });
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"part":"input","insidePopup":false,"inline":false,"disabled":false,"readOnly":false,"multiple":false,"hasValue":false,"virtualized":false,"valueRevision":0,"open":false,"mounted":false,"rtl":false,"touchOpened":false,"decidesKeyDown":false,"decidesPress":false,"focusOnAttach":false}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ComboboxTwin>>(attach.Arguments[2]);
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"part":"input","insidePopup":true,"inline":true,"disabled":true,"readOnly":true,"multiple":true,"hasValue":true,"virtualized":true,"chipsId":"c","listId":"l","popupId":"p","value":"v","valueRevision":3,"open":true,"mounted":true,"rtl":true,"inputId":"i","touchOpened":true,"chipIds":["a","b"],"index":2,"dismissId":"d","submitRevision":4,"submitFormElement":0,"decidesKeyDown":true,"decidesPress":true,"focusOnAttach":true}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMB-KEY-17")]
    [Trait("Requirement", "CMB-KEY-05")]
    [Trait("Requirement", "CMB-KEY-04")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task The_reports_raise_their_events_with_the_parsed_payloads()
    {
        using BunitContext context = new();
        await using ComboboxTwin twin = new(context.JSInterop.JSRuntime);
        List<string> received = [];
        twin.InputChanged += change => received.Add($"input:{change.Text}:{change.Typed}:{change.Composing}");
        twin.ClearRequested += request => received.Add($"clear:{request.Index}:{request.Event.Key}");
        twin.RemoveRequested += request => received.Add($"remove:{request.Index}");
        twin.OpenCanceled += snapshot => received.Add($"cancel:{snapshot.Type}");
        twin.DismissPressed += snapshot => received.Add($"dismiss:{snapshot.Type}");
        twin.SelectionEventReported += snapshot => received.Add($"selection:{snapshot?.Key}");
        twin.EnterCloseRequested += snapshot => received.Add($"enter:{snapshot.Key}");
        twin.InputPressed += snapshot => received.Add($"press:{snapshot.Type}");
        twin.OpenRequested += snapshot => received.Add($"open:{snapshot.Key}");
        twin.ItemLinkPressed += href => received.Add($"link:{href ?? "end"}");
        twin.KeyHandled += snapshot => received.Add($"key:{snapshot.Key}");
        twin.Blurred += snapshot => received.Add($"blur:{snapshot.Type}");

        twin.OnInputChange($$"""{"text":"ab","typed":true,"composing":false,"event":{{KeyJson}}}""");
        twin.OnClear($$"""{"index":-1,"event":{{KeyJson}}}""");
        twin.OnRemove($$"""{"index":2,"event":{{KeyJson}}}""");
        twin.OnCancelOpen(KeyJson);
        twin.OnDismiss(KeyJson);
        twin.OnSelectionEvent($$"""{"event":{{KeyJson}}}""");
        twin.OnEnterClose($$"""{"event":{{KeyJson}}}""");
        twin.OnInputPress(KeyJson);
        twin.OnOpenRequest(KeyJson);
        twin.OnItemLinkPress("""{"href":"#a"}""");
        twin.OnItemLinkPress("""{"href":null}""");
        twin.OnKeyHandled(KeyJson);
        twin.OnBlur(KeyJson);

        Assert.Equal(["input:ab:True:False", "clear:-1:Escape", "remove:2", "cancel:keydown", "dismiss:keydown", "selection:Escape", "enter:Escape", "press:keydown", "open:Escape", "link:#a", "link:end", "key:Escape", "blur:keydown"], received);
    }

    // ADR 04 Amendments 2026-11-11 (the Chip's consumer handlers, option (a) settled by the orchestrator 2026-11-11): a Chip with
    // a consumer onkeydown or onmousedown attaches the `chip` part, which asks `navigate` and `press` and, on Server, reports the
    // event so C# runs the handler.
    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMB-KEY-10")]
    [Trait("Requirement", "CMB-PTR-08")]
    public async Task The_chip_part_reports_its_event_and_its_points_ask_the_decider()
    {
        using BunitContext context = new();
        await using ComboboxTwin twin = new(context.JSInterop.JSRuntime);
        List<string> received = [];
        twin.ChipEventReported += snapshot => received.Add($"chip:{snapshot.Type}:{snapshot.Key}");
        twin.Decider = (point, snapshot) =>
        {
            received.Add($"decide:{point}:{snapshot.Key}");
            return DecisionPolicy.Stop;
        };

        twin.OnChipEvent(KeyJson);
        string verdict = twin.Decide(ComboboxTwin.NavigatePoint, KeyJson);

        Assert.Equal("chip", ComboboxTwin.ChipPart);
        Assert.Equal("stop", verdict);
        Assert.Equal(["chip:keydown:Escape", "decide:navigate:Escape"], received);
    }

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMB-KEY-05")]
    [Trait("Requirement", "CMB-EVT-09")]
    public async Task The_clear_and_remove_points_ask_the_decider_and_run_without_one()
    {
        using BunitContext context = new();
        await using ComboboxTwin twin = new(context.JSInterop.JSRuntime);
        Assert.Equal("run", twin.Decide(ComboboxTwin.ClearPoint, KeyJson));
        List<string> asked = [];
        twin.Decider = (point, snapshot) =>
        {
            asked.Add(point + ":" + snapshot.Key);
            return DecisionPolicy.Stop;
        };

        Assert.Equal("stop", twin.Decide(ComboboxTwin.ClearPoint, KeyJson));
        Assert.Equal("stop", twin.Decide(ComboboxTwin.RemovePoint, KeyJson));
        Assert.Equal(["clear:Escape", "remove:Escape"], asked);
    }

    // ADR 04 Amendments 2026-11-10 (design point 6, chosen by the user): the consumer's onKeyDown veto on the Input and
    // onMouseDown veto on the Chips and the InputGroup, falling back to `run`.
    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CMB-PTR-08")]
    public async Task The_navigate_and_press_points_ask_the_decider_and_run_without_one()
    {
        using BunitContext context = new();
        await using ComboboxTwin twin = new(context.JSInterop.JSRuntime);
        Assert.Equal("navigate", ComboboxTwin.NavigatePoint);
        Assert.Equal("press", ComboboxTwin.PressPoint);
        Assert.Equal("run", twin.Decide(ComboboxTwin.NavigatePoint, KeyJson));
        Assert.Equal("run", twin.Decide(ComboboxTwin.PressPoint, KeyJson));
        List<string> asked = [];
        twin.Decider = (point, snapshot) =>
        {
            asked.Add(point + ":" + snapshot.Type);
            return DecisionPolicy.Stop;
        };

        Assert.Equal("stop", twin.Decide(ComboboxTwin.NavigatePoint, KeyJson));
        Assert.Equal("stop", twin.Decide(ComboboxTwin.PressPoint, KeyJson));
        Assert.Equal(["navigate:keydown", "press:keydown"], asked);
    }
}
