using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Collapsible panel pieces under Accordion's names and rules (M3 track 6, edit (c); ADR 04 Amendments 2026-10-26 for
/// the variable names, chosen by the user; the empty-id policy and the messages settled by the orchestrator 2026-10-25):
/// <see cref="CollapsiblePanelBehavior"/> composes its <c>auto</c> style from the variables it was given and sends their
/// names, keeps an empty <c>id</c> under the Accordion policy (ACC-API-18) and reports the message its caller chose
/// (ACC-EDGE-04/05, TRN-EDGE-04). Collapsible's own JSON and rules are pinned by the Collapsible suites.
/// </summary>
public sealed class AccordionPanelBehaviorTests : IDisposable
{
    private const string CollapsibleModulePath = "./_content/Blazix.Primitives/js/collapsible.js";

    private readonly BunitContext context = new();
    private readonly BunitJSModuleInterop module;
    private readonly CapturingLogger<AccordionPanelBehaviorTests> logger = new();

    public AccordionPanelBehaviorTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        module = context.JSInterop.SetupModule(CollapsibleModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: the accordion panel composes its auto style from the accordion variables and sends their names")]
    [Trait("Requirement", "ACC-DOM-17")]
    [Trait("Requirement", "ACC-DOM-18")]
    public async Task The_accordion_panel_composes_its_auto_style_from_the_accordion_variables()
    {
        CollapsibleRootContext root = Root(open: true);
        using CollapsiblePanelBehavior behavior = Behavior(root, CollapsiblePanelVariables.Accordion);
        behavior.SetId(null);
        AttributeList attributes = Render(behavior, root, consumerStyle: "color: red");

        Assert.True(attributes.TryGetValue("style", out AttributeValue style));
        Assert.True(style.TryGetValue(out string text));
        Assert.Equal("--accordion-panel-height: auto; --accordion-panel-width: auto; color: red; animation-name: none;", text);

        await behavior.AfterRenderAsync(rendered: true, element: null);
        string options = Assert.IsType<string>(Assert.Single(module.Invocations["attach"]).Arguments[1]);
        Assert.Contains("\"heightVariable\":\"--accordion-panel-height\",\"widthVariable\":\"--accordion-panel-width\"", options, StringComparison.Ordinal);
        Assert.DoesNotContain("collapsible-panel", options, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: under the accordion policy an empty id renders as empty and registers nothing")]
    [Trait("Requirement", "ACC-API-18")]
    [Trait("Requirement", "ACC-DOM-19")]
    public void Under_the_accordion_policy_an_empty_id_renders_as_empty_and_registers_nothing()
    {
        CollapsibleRootContext root = Root(open: true);
        using CollapsiblePanelBehavior behavior = Behavior(root, CollapsiblePanelVariables.Accordion);
        behavior.RendersEmptyId = true;
        behavior.SetId(string.Empty);
        AttributeList attributes = Render(behavior, root, consumerId: string.Empty);

        Assert.True(attributes.TryGetValue("id", out AttributeValue id));
        Assert.True(id.TryGetValue(out string text));
        Assert.Equal(string.Empty, text);
        // The trigger's aria-controls keeps naming the generated panel id, which no element carries.
        Assert.Equal("base-ui-panel", root.State.PanelId);
    }

    [Fact(DisplayName = "port: the panel reports the keepMounted message its caller chose, and the accordion messages name their parts")]
    [Trait("Requirement", "ACC-EDGE-04")]
    [Trait("Requirement", "ACC-EDGE-05")]
    public void The_panel_reports_the_keepMounted_message_its_caller_chose()
    {
        CollapsibleRootContext root = Root(open: false);
        using CollapsiblePanelBehavior behavior = Behavior(root, CollapsiblePanelVariables.Accordion);
        behavior.KeepMountedIgnoredMessage = CollapsibleDevelopmentLog.AccordionPanelKeepMountedIgnoredMessage;

        behavior.ReportKeepMountedIgnored();

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Equal(
            "Blazix: The `KeepMounted` parameter set to `false` on an `AccordionPanel` is ignored when `HiddenUntilFound` is enabled on the panel or root, since the panel must remain mounted while closed.",
            message);
        Assert.Equal(
            "Blazix: The `KeepMounted` parameter set to `false` on `AccordionRoot` is ignored when `HiddenUntilFound` is enabled, since panels must remain mounted while closed.",
            CollapsibleDevelopmentLog.AccordionRootKeepMountedIgnoredMessage);
    }

    // Edit (d); D1 option (a), pending the user: an Accordion Item has no root twin, so the panel's report is its status.
    [Fact(DisplayName = "port: a panel that reports its status asks the module for it and mirrors each report into the shared state once")]
    [Trait("Requirement", "ACC-STA-03")]
    [Trait("Requirement", "ACC-STA-09")]
    public async Task A_panel_that_reports_its_status_mirrors_each_report_into_the_shared_state()
    {
        CollapsibleRootContext root = Root(open: false);
        using CollapsiblePanelBehavior behavior = Behavior(root, CollapsiblePanelVariables.Accordion);
        behavior.ReportsStatus = true;
        behavior.KeepMounted = true;
        behavior.SetId(null);
        int notifications = 0;
        using IDisposable subscription = root.Subscribe(_ => notifications++);
        _ = Render(behavior, root);
        await behavior.AfterRenderAsync(rendered: true, element: null);
        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Contains("\"reportStatus\":true", Assert.IsType<string>(attach.Arguments[1]), StringComparison.Ordinal);
        CollapsibleTwin twin = ((DotNetObjectReference<CollapsibleTwin>)attach.Arguments[2]!).Value;

        root.OpenState.Update(true, true, false, false);
        root.ApplyOpen(true);
        Assert.Equal(TransitionStatus.Starting, root.State.TransitionStatus);
        int before = notifications;
        twin.OnPanelStatusChange("""{"status":"idle"}""");
        twin.OnPanelStatusChange("""{"status":"idle"}""");

        Assert.Equal(TransitionStatus.Idle, root.State.TransitionStatus);
        Assert.Equal(before + 1, notifications);

        // The close's ending, then the exit's end: the shared status ends unset, as a collapsible root's does.
        root.OpenState.Update(true, false, false, false);
        root.ApplyOpen(false);
        twin.OnPanelStatusChange("""{"status":"ending"}""");
        twin.OnTransitionComplete("""{"open":false}""");
        Assert.False(root.State.Mounted);
        Assert.Null(root.State.TransitionStatus);
    }

    private static CollapsibleRootContext Root(bool open)
    {
        CollapsibleRootContext root = new("base-ui-panel", logger: null);
        root.OpenState.Update(true, open, false, false);
        root.ApplyParameters(disabled: false, firstSet: true);
        return root;
    }

    private CollapsiblePanelBehavior Behavior(CollapsibleRootContext root, CollapsiblePanelVariables variables) =>
        new(root, context.JSInterop.JSRuntime, new CollapsibleDevelopmentLog(), logger, static () => { }, static _ => Task.CompletedTask, variables);

    // What the panel part does in one render: its own attributes, the consumer's merged over them, then the finish.
    private static AttributeList Render(CollapsiblePanelBehavior behavior, CollapsibleRootContext root, string? consumerStyle = null, string? consumerId = null)
    {
        CollapsibleRootStoreState state = root.State;
        AttributeList attributes = new();
        behavior.AddAttributes(attributes, state.Open, state.Mounted, behavior.Status(state.TransitionStatus));
        if (consumerId is not null)
        {
            attributes.Set("id", consumerId);
        }

        if (consumerStyle is not null)
        {
            attributes.Set("style", consumerStyle);
        }

        behavior.FinishAttributes(attributes, state.Open);
        return attributes;
    }
}
