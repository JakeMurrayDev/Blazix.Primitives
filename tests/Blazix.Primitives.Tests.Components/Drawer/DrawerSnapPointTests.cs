using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The snap point titles of <c>DrawerRoot.test.tsx</c> (titles verbatim) that need no gesture: the active snap point, the
/// request sequence and the close wrapper are C#'s (decision 7 (a) of the M6 plan, chosen by the user 2026-11-18;
/// DRW-STA-02..04, API-03..05), the list, offsets and geometry the <c>drawer</c> module's (8b).
/// </summary>
public sealed class DrawerSnapPointTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "resets the active snap point when closing")]
    [Trait("Requirement", "DRW-STA-04")]
    [Trait("Requirement", "DRW-API-04")]
    public async Task Resets_the_active_snap_point_when_closing()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>();
        Assert.Equal("1", cut.Find("[data-testid=active-snap]").TextContent);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        Assert.Equal("100px", cut.Find("[data-testid=active-snap]").TextContent);
    }

    [Fact(DisplayName = "resets to the default snap point when provided")]
    [Trait("Requirement", "DRW-STA-04")]
    [Trait("Requirement", "DRW-API-04")]
    [Trait("Requirement", "DRW-STA-03")]
    public async Task Resets_to_the_default_snap_point_when_provided()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "default"));
        Assert.Equal("300px", cut.Find("[data-testid=active-snap]").TextContent);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        Assert.Equal("300px", cut.Find("[data-testid=active-snap]").TextContent);
        // DRW-STA-03: no equality guard, a request for the current value still calls OnSnapPointChange.
        Assert.Equal((SnapPointValue)"300px", Assert.Single(cut.Instance.Requests).Value);
    }

    [Fact(DisplayName = "provides event details when snap point changes")]
    [Trait("Requirement", "DRW-API-05")]
    [Trait("Requirement", "DRW-STA-04")]
    public async Task Provides_event_details_when_snap_point_changes()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>();

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        ValueChangeEventArgs<SnapPointValue?> request = Assert.Single(cut.Instance.Requests);
        Assert.Equal(EventReason.ClosePress, request.Details.Reason);
        Assert.IsNotType<OpenChangeEventDetails>(request.Details);
    }

    [Fact(DisplayName = "does not reset snap point when a close is canceled")]
    [Trait("Requirement", "DRW-STA-04")]
    public async Task Does_not_reset_snap_point_when_a_close_is_canceled()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "canceled-close"));
        Assert.Equal("1", cut.Find("[data-testid=active-snap]").TextContent);

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        Assert.Equal("1", cut.Find("[data-testid=active-snap]").TextContent);
        Assert.Empty(cut.Instance.Requests);
        Assert.NotNull(cut.Find("[data-testid=popup]").GetAttribute("data-open"));
    }

    [Fact(DisplayName = "honors canceled snap point changes and falls back from invalid uncontrolled values")]
    [Trait("Requirement", "DRW-STA-02")]
    [Trait("Requirement", "DRW-STA-03")]
    [Trait("Requirement", "DRW-API-05")]
    public async Task Honors_canceled_snap_point_changes_and_falls_back_from_invalid_uncontrolled_values()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "context"));
        Assert.Equal("300px", cut.Find("[data-testid=context-active-snap-point]").TextContent);

        await cut.Find("[data-testid=set-canceled]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal("300px", cut.Find("[data-testid=context-active-snap-point]").TextContent);

        await cut.Find("[data-testid=set-invalid]").ClickAsync(new MouseEventArgs { Detail = 1 });
        Assert.Equal("300px", cut.Find("[data-testid=context-active-snap-point]").TextContent);
        ValueChangeEventArgs<SnapPointValue?> last = cut.Instance.Requests[^1];
        Assert.Equal((SnapPointValue)999, last.Value);
        Assert.Equal(EventReason.None, last.Details.Reason);
    }

    // The probe's binding title (ADR 08 Amendments 2026-11-18, the probe's outcome): a number literal, a string expression,
    // a list of both and @bind-SnapPoint against a SnapPointValue? field.
    [Fact(DisplayName = "port: SnapPoints, SnapPoint and DefaultSnapPoint bind a number, a string and a list of both")]
    [Trait("Requirement", "DRW-API-02")]
    [Trait("Requirement", "DRW-API-03")]
    [Trait("Requirement", "DRW-API-04")]
    public void SnapPoints_SnapPoint_and_DefaultSnapPoint_bind_a_number_a_string_and_a_list()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "bind"));

        Assert.Equal("200px", cut.Find("[data-testid=active-snap]").TextContent);
        Assert.Equal("0.5", cut.Find("[data-testid=literal-snap]").TextContent);
        Assert.Equal("1", cut.Find("[data-testid=default-snap]").TextContent);
    }

    // DRW-STA-16 (a `none` row; this is the port title that can fail): only the number 1 expands the popup.
    [Fact(DisplayName = "port: the popup is expanded at the number 1 and not at the string \"1\" or another value")]
    [Trait("Requirement", "DRW-STA-16")]
    [Trait("Requirement", "DRW-DOM-02")]
    public async Task The_popup_is_expanded_only_at_the_number_1()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>();
        Assert.True(cut.Find("[data-testid=popup]").HasAttribute("data-expanded"));

        await cut.Find("[data-testid=close]").ClickAsync(new MouseEventArgs { Detail = 1 });

        Assert.False(cut.Find("[data-testid=popup]").HasAttribute("data-expanded"));
    }

    // ADR 08 Amendments 2026-11-18: SnapPoints is compared by sequence, so an equal list in a new instance notifies no part
    // (and, from 8b, sends the module no `update`); a parent's render re-renders its content in Blazor either way.
    [Fact(DisplayName = "port: an equal snap point list in a new instance notifies no part, and a different list does")]
    [Trait("Requirement", "DRW-API-02")]
    public void An_equal_snap_point_list_notifies_no_part()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "default"));
        int notified = 0;
        using IDisposable subscription = cut.FindComponent<DrawerRoot<NoPayload>>().Instance.DrawerContext.Subscribe(DrawerFields.Settings, () => notified++);

        cut.Render(p => p.Add(x => x.Shape, "default").Add(x => x.SnapPointList, new List<SnapPointValue> { "100px", "300px", 1 }));
        Assert.Equal(0, notified);

        cut.Render(p => p.Add(x => x.Shape, "default").Add(x => x.SnapPointList, new List<SnapPointValue> { "100px", "300px", "1" }));
        Assert.Equal(1, notified);
    }

    // DRW-API-03 (UTL useControlled, name 'Drawer', state 'snapPoint').
    [Fact(DisplayName = "port: switching the snap point between controlled and uncontrolled logs the controlled-state warning")]
    [Trait("Requirement", "DRW-API-03")]
    public void Switching_the_snap_point_between_controlled_and_uncontrolled_warns()
    {
        CapturingLogger<DrawerRoot<NoPayload>> logger = new();
        test.Context.Services.AddSingleton<Microsoft.Extensions.Logging.ILogger<DrawerRoot<NoPayload>>>(logger);
        IRenderedComponent<DrawerRoot<NoPayload>> cut = test.Context.Render<DrawerRoot<NoPayload>>(p => p.Add(x => x.SnapPoint, (SnapPointValue?)"100px"));

        cut.Render(p => p.Add(x => x.DefaultOpen, false));

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("controlled snapPoint state of Drawer to be uncontrolled", StringComparison.Ordinal));
    }
}
