using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Portal, Positioner, Popup, Arrow and Backdrop at L2 (PreviewCardPopup.test.tsx, PreviewCardBackdrop.test.tsx and the
/// positioner rows of PreviewCardPositioner.test.tsx): the popup's children, the portal's mounting, the positioner's
/// defaults, inert style and inline middleware, the arrow's attributes and the backdrop's inert style.
/// </summary>
public sealed class PreviewCardPopupPartsTests : IDisposable
{
    private readonly PreviewCardTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "should render the children")]
    [Trait("Requirement", "PRV-STR-05")]
    public void Should_render_the_children()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        Assert.Contains("Content", cut.Find("[data-testid=\"popup\"]").TextContent, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "sets `pointer-events: none` style")]
    [Trait("Requirement", "PRV-DOM-18")]
    [Trait("Requirement", "PRV-DOM-19")]
    public void Sets_pointer_events_none_style()
    {
        IRenderedComponent<PreviewCardRoot<NoPayload>> cut = host.Context.Render<PreviewCardRoot<NoPayload>>(p => p.Add(x => x.Open, true).Add(x => x.ChildContent, (RenderFragment)(builder =>
        {
            builder.OpenComponent<PreviewCardBackdrop>(0);
            builder.AddAttribute(1, "data-testid", "backdrop");
            builder.CloseComponent();
        })));

        AngleSharp.Dom.IElement backdrop = cut.Find("[data-testid=\"backdrop\"]");
        Assert.Contains("pointer-events: none", backdrop.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Contains("user-select: none", backdrop.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal("presentation", backdrop.GetAttribute("role"));
        Assert.True(backdrop.HasAttribute("data-open"));
        Assert.False(backdrop.HasAttribute("data-side"));
    }

    [Fact(DisplayName = "port: the card unmounts its portal when closed by default")]
    [Trait("Requirement", "PRV-API-16")]
    [Trait("Requirement", "PRV-DOM-06")]
    public async Task Unmounts_the_closed_card_by_default()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));
        Assert.NotNull(cut.Find("[data-base-ui-portal]"));

        await cut.InvokeAsync(() => cut.Instance.Root!.CloseAsync());
        await cut.InvokeAsync(cut.Instance.Root!.Unmount);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-base-ui-portal]")));
        Assert.Empty(cut.FindAll("[data-testid=\"positioner\"]"));
    }

    [Fact(DisplayName = "port: a kept-mounted card renders its closed positioner as hidden")]
    [Trait("Requirement", "PRV-API-16")]
    [Trait("Requirement", "PRV-DOM-06")]
    [Trait("Requirement", "PRV-DOM-07")]
    public void A_kept_mounted_card_renders_its_closed_positioner_as_hidden()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.KeepMounted, true));

        AngleSharp.Dom.IElement positioner = cut.Find("[data-testid=\"positioner\"]");
        Assert.True(positioner.HasAttribute("hidden"));
        Assert.True(positioner.HasAttribute("data-closed"));
        Assert.True(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "port: the positioner renders role presentation and PreviewCard's default side, and sends its paddings and the hovered-line option")]
    [Trait("Requirement", "PRV-API-18")]
    [Trait("Requirement", "PRV-POS-01")]
    [Trait("Requirement", "PRV-POS-03")]
    [Trait("Requirement", "PRV-DOM-05")]
    [Trait("Requirement", "PRV-DOM-09")]
    public void The_positioner_renders_PreviewCards_defaults()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement positioner = cut.Find("[data-testid=\"positioner\"]");
        Assert.Equal("presentation", positioner.GetAttribute("role"));
        Assert.Equal("bottom", positioner.GetAttribute("data-side"));
        Assert.Equal("center", positioner.GetAttribute("data-align"));
        Assert.True(positioner.HasAttribute("data-open"));
        Assert.False(positioner.HasAttribute("hidden"));
        Assert.False(positioner.HasAttribute("data-instant"));
        Assert.DoesNotContain("pointer-events", positioner.GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
        JsonElement options = PositioningOptions();
        Assert.Equal("bottom", options.GetProperty("side").GetString());
        Assert.Equal(5, options.GetProperty("collisionPadding").GetDouble());
        Assert.Equal(5, options.GetProperty("arrowPadding").GetDouble());
        Assert.True(options.GetProperty("hoveredLine").GetBoolean());
        Assert.Equal("trigger", options.GetProperty("triggerId").GetString());
        Assert.Equal(cut.Instance.Root!.Store.SessionToken, options.GetProperty("sessionToken").GetString());
    }

    [Fact(DisplayName = "port: the positioner's floating handle joins the card's session with the hover leave and the tree")]
    [Trait("Requirement", "PRV-POS-02")]
    [Trait("Requirement", "PRV-PTR-06")]
    [Trait("Requirement", "PRV-STR-10")]
    public void The_positioners_floating_handle_joins_the_session()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        // PreviewCard's floating handle takes part in the tree (no `node: false`, unlike Tooltip's) and carries the hover
        // leave with the store's close delay.
        string? floating = host.InteractionsAttaches.Select(attach => (string)attach.Arguments[1]!).Concat(host.InteractionsUpdates).LastOrDefault(json => json.Contains("\"floating\":", StringComparison.Ordinal));
        Assert.NotNull(floating);
        JsonElement options = JsonDocument.Parse(floating).RootElement.GetProperty("floating");
        Assert.False(options.TryGetProperty("node", out _));
        Assert.Equal(300, options.GetProperty("hover").GetProperty("closeDelay").GetInt32());
        Assert.Equal(cut.Instance.Root!.Store.SessionToken, JsonDocument.Parse(floating).RootElement.GetProperty("sessionToken").GetString());
    }

    [Fact(DisplayName = "is hidden from assistive technology and mirrors the resolved side")]
    [Trait("Requirement", "PRV-ARIA-01")]
    [Trait("Requirement", "PRV-DOM-16")]
    [Trait("Requirement", "PRV-DOM-17")]
    public void Is_hidden_from_assistive_technology_and_mirrors_the_resolved_side()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p.Add(x => x.DefaultOpen, true));

        AngleSharp.Dom.IElement arrow = cut.Find("[data-testid=\"arrow\"]");
        Assert.Equal("true", arrow.GetAttribute("aria-hidden"));
        Assert.True(arrow.HasAttribute("data-open"));
        Assert.Equal("bottom", arrow.GetAttribute("data-side"));
        Assert.False(arrow.HasAttribute("data-instant"));
        Assert.Contains("position: absolute", arrow.GetAttribute("style"), StringComparison.Ordinal);
        Assert.Equal(arrow.Id, PositioningOptions().GetProperty("arrowId").GetString());
    }

    // PRV-DOM-14's TRN-DOM-05 clause (ADR 04 Amendments 2026-10-28, gap 13 of M4 track 5): C# renders `transition: none`
    // while starting until the transitions handle attaches; from attach the module writes it into its own band.
    [Fact(DisplayName = "port: the popup renders transition: none while starting, and its transitions handle writes it from attach")]
    [Trait("Requirement", "PRV-DOM-14")]
    public async Task The_popup_renders_transition_none_while_starting_and_hands_it_to_its_transitions_handle()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>();

        await cut.InvokeAsync(() => cut.Instance.Root!.Store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerHover)));

        Assert.Contains("transition: none", cut.Find("[data-testid=\"popup\"]").GetAttribute("style") ?? string.Empty, StringComparison.Ordinal);
        JSRuntimeInvocation attach = host.Context.JSInterop.Invocations.Last(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal));
        Assert.True(JsonDocument.Parse((string)attach.Arguments[1]!).RootElement.GetProperty("disableTransitionWhileStarting").GetBoolean());
    }

    // Issue 147 ((B1) with issue 148, ADR 04 Amendments 2026-10-29), as Tooltip's TIP-EVT-04: the PreventUnmountOnClose()
    // call announces the retained close through the transitions module's export, addressed by the element, ahead of any
    // render the consumer's callback makes; a close the callback then cancels sends the reset.
    [Fact(DisplayName = "port: the PreventUnmountOnClose() call announces the retained close to the transitions module before any render")]
    [Trait("Requirement", "PRV-ANI-06")]
    [Trait("Requirement", "PRV-EVT-07")]
    public async Task The_call_announces_the_retained_close_before_any_render()
    {
        List<bool> announcedAtCall = [];
        IRenderedComponent<PreviewCardBasicHost>? cut = null;
        cut = host.Context.Render<PreviewCardBasicHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    announcedAtCall.Add(RetainExitAnnouncements().SequenceEqual([true]) && cut!.Find("[data-testid=\"popup\"]").HasAttribute("data-open"));
                }
            }));
        cut.WaitForAssertion(() => Assert.NotNull(LastTransitionsOptions(reportsOpenComplete: true)));

        await cut.InvokeAsync(() => cut.Instance.Root!.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerHover)));

        Assert.Equal([true], announcedAtCall);
        Assert.Equal([true], RetainExitAnnouncements());
    }

    [Fact(DisplayName = "port: a retained close the callback then cancels sends the announcement's reset")]
    [Trait("Requirement", "PRV-ANI-06")]
    [Trait("Requirement", "PRV-EVT-07")]
    public async Task A_canceled_retained_close_sends_the_reset()
    {
        IRenderedComponent<PreviewCardBasicHost> cut = host.Context.Render<PreviewCardBasicHost>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    args.Details.Cancel();
                }
            }));
        cut.WaitForAssertion(() => Assert.NotNull(LastTransitionsOptions(reportsOpenComplete: true)));

        await cut.InvokeAsync(() => cut.Instance.Root!.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerHover)));

        Assert.Equal([true, false], RetainExitAnnouncements());
    }

    // As Popover's backdrop: the backdrop's own transitions handle keeps a retained close's ending phase as the popup's does.
    [Fact(DisplayName = "port: the backdrop announces a retained close and carries it in its transitions options")]
    [Trait("Requirement", "PRV-ANI-06")]
    public async Task The_backdrop_announces_a_retained_close_and_carries_it_in_its_options()
    {
        IRenderedComponent<PreviewCardRoot<NoPayload>> cut = host.Context.Render<PreviewCardRoot<NoPayload>>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                }
            })
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<PreviewCardBackdrop>(0);
                builder.AddAttribute(1, "data-testid", "backdrop");
                builder.CloseComponent();
            })));
        cut.WaitForAssertion(() => Assert.NotNull(LastTransitionsOptions(reportsOpenComplete: false)));
        string backdrop = host.Context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"reportOpenComplete\":false", StringComparison.Ordinal))
            .Select(invocation => ((ElementReference)invocation.Arguments[0]!).Id)
            .Single();

        await cut.InvokeAsync(() => cut.Instance.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.TriggerHover)));

        cut.WaitForAssertion(() => Assert.Contains("\"retainExit\":true", LastTransitionsOptions(reportsOpenComplete: false), StringComparison.Ordinal));
        Assert.Contains(
            host.Context.JSInterop.Invocations,
            invocation => invocation.Identifier == "retainExit" && invocation.Arguments[0] is ElementReference element && element.Id == backdrop && (bool)invocation.Arguments[1]!);
    }

    // Issue 149's class for the backdrop: inside the portal it creates its transitions twin after the move, and a backdrop
    // disposed while the move is pending creates none.
    [Fact(DisplayName = "port: a backdrop disposed while its portal's move is pending creates no transitions twin")]
    [Trait("Requirement", "TRN-ALG-01")]
    public async Task A_backdrop_disposed_while_its_portals_move_is_pending_creates_no_transitions_twin()
    {
        HeldAttachRuntime runtime = new(host.Context.JSInterop.JSRuntime, "portal.js");
        host.Context.Services.AddSingleton<IJSRuntime>(runtime);
        host.Context.Render<PreviewCardRoot<NoPayload>>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.ChildContent, (RenderFragment)(builder =>
            {
                builder.OpenComponent<PreviewCardPortal>(0);
                builder.AddAttribute(1, nameof(PreviewCardPortal.ChildContent), (RenderFragment)(portal =>
                {
                    portal.OpenComponent<PreviewCardBackdrop>(0);
                    portal.CloseComponent();
                }));
                builder.CloseComponent();
            })));
        int before = runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal));

        await host.Context.DisposeComponentsAsync();

        // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016
        runtime.Release();
#pragma warning restore BL0016
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(before, runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal)));
    }

    private bool[] RetainExitAnnouncements() =>
        [.. host.Context.JSInterop.Invocations.Where(invocation => invocation.Identifier == "retainExit").Select(invocation => (bool)invocation.Arguments[1]!)];

    // The popup's transitions options report the open's completion; the backdrop's do not.
    private string? LastTransitionsOptions(bool reportsOpenComplete) =>
        host.Context.JSInterop.Invocations
            .Select(invocation => invocation.Identifier switch
            {
                "attach" when invocation.Arguments.Count > 1 => invocation.Arguments[1] as string,
                "update" => invocation.Arguments[0] as string,
                _ => null,
            })
            .LastOrDefault(json => json is not null && json.Contains("\"openSelector\"", StringComparison.Ordinal) && json.Contains(reportsOpenComplete ? "\"reportOpenComplete\":true" : "\"reportOpenComplete\":false", StringComparison.Ordinal));

    private JsonElement PositioningOptions()
    {
        JSRuntimeInvocation attach = host.Context.JSInterop.Invocations.Last(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal) is false && json.Contains("\"collisionPadding\"", StringComparison.Ordinal));
        string last = (string)attach.Arguments[1]!;
        foreach (JSRuntimeInvocation update in host.Context.JSInterop.Invocations.Where(invocation => invocation.Identifier == "update" && invocation.Arguments.Count > 0 && invocation.Arguments[0] is string json && json.Contains("\"collisionPadding\"", StringComparison.Ordinal)))
        {
            last = (string)update.Arguments[0]!;
        }

        return JsonDocument.Parse(last).RootElement;
    }
}
