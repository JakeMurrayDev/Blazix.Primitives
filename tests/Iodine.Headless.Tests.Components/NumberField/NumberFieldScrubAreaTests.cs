using System.Globalization;
using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// 6b's scrub role at L2 (issue 277): the ScrubArea and ScrubAreaCursor parts over the `number-field` module's scrub
// reports (ADR 06 §1). NumberFieldScrubAreaCursor.test.tsx's titles drive the start, lock and end reports where Base UI
// drives a pointer; the pointer itself, the lock and the moves are L3 (number-field.scrub.test.ts) and L4
// (number-field.scrub.spec.ts).
/// <summary>The scrub area and its cursor over the module's scrub reports.</summary>
public sealed class NumberFieldScrubAreaTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly List<ValueChangeEventArgs<double?>> changes = [];
    private readonly List<ValueCommittedEventArgs<double?>> commits = [];

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "has presentation role")]
    [Trait("Requirement", "NUM-DOM-12")]
    public void Has_presentation_role()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.CloseComponent();
        });

        Assert.Single(cut.FindAll("[role=presentation]"));
    }

    [Fact(DisplayName = "port: the scrub area's inline style keeps touch and selection off under the consumer's style")]
    [Trait("Requirement", "NUM-DOM-12")]
    public void The_scrub_areas_inline_style_is_under_the_consumers()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.AddAttribute(1, "style", "cursor: ew-resize; touch-action: pan-y");
            b.CloseComponent();
        });

        Assert.Equal(
            "touch-action: pan-y; -webkit-user-select: none; user-select: none; cursor: ew-resize;",
            cut.Find("span[role=presentation]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the scrub area sends its direction, sensitivity and teleport distance, omitting the defaults")]
    [Trait("Requirement", "NUM-API-24")]
    public void The_scrub_area_sends_its_options()
    {
        Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            b.OpenComponent<NumberFieldScrubArea>(1);
            b.CloseComponent();
            b.OpenComponent<NumberFieldScrubArea>(2);
            b.AddComponentParameter(3, nameof(NumberFieldScrubArea.Direction), Orientation.Vertical);
            b.AddComponentParameter(4, nameof(NumberFieldScrubArea.PixelSensitivity), 5d);
            b.AddComponentParameter(5, nameof(NumberFieldScrubArea.TeleportDistance), 100d);
            b.CloseComponent();
        });

        Assert.Equal(
            ["""{"part":"scrub"}""", """{"part":"scrub","vertical":true,"pixelSensitivity":5,"teleportDistance":100}"""],
            test.ScrubOptions);
    }

    [Fact(DisplayName = "port: a scrub sets data-scrubbing on every part, steps by the delta with reason scrub, and the release commits once")]
    [Trait("Requirement", "NUM-STA-15")]
    [Trait("Requirement", "NUM-DOM-02")]
    [Trait("Requirement", "NUM-PTR-11")]
    [Trait("Requirement", "NUM-PTR-13")]
    [Trait("Requirement", "NUM-EVT-05")]
    public async Task A_scrub_sets_scrubbing_steps_and_commits_once()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        // The root, the input, the scrub area and the cursor.
        Assert.Equal(4, cut.FindAll("[data-scrubbing]").Count);

        await test.ScrubStepAsync(cut, -10);
        await test.ScrubStepAsync(cut, 5);
        Assert.Equal("-5", test.Text);
        Assert.Equal([EventReason.Scrub, EventReason.Scrub], changes.Select(c => c.Details.Reason));
        Assert.Equal([-1, 1], changes.Select(c => ((NumberFieldChangeEventDetails)c.Details).Direction));
        Assert.Empty(commits);

        await test.ScrubEndAsync(cut);

        Assert.Empty(cut.FindAll("[data-scrubbing]"));
        ValueCommittedEventArgs<double?> commit = Assert.Single(commits);
        Assert.Equal(-5, commit.Value);
        Assert.Equal(EventReason.Scrub, commit.Details.Reason);
    }

    [Fact(DisplayName = "port: a release with no step still commits the current value (NUM-PTR-13)")]
    [Trait("Requirement", "NUM-PTR-13")]
    [Trait("Requirement", "NUM-EVT-05")]
    public async Task A_release_with_no_step_commits()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(7);

        await test.ScrubStartAsync(cut);
        await test.ScrubEndAsync(cut);

        Assert.Empty(changes);
        Assert.Equal(7, Assert.Single(commits).Value);
    }

    [Fact(DisplayName = "port: a scrub step uses the move's modifiers and echoes the step's revision to the input")]
    [Trait("Requirement", "NUM-STA-16")]
    [Trait("Requirement", "NUM-PTR-11")]
    public async Task A_scrub_step_uses_modifiers_and_echoes_the_revision()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 2, shiftKey: true);
        await test.ScrubStepAsync(cut, 1, altKey: true);

        Assert.Equal("20.1", test.Text);
        Assert.Equal(2, JsonDocument.Parse(test.NumberFieldOptions[^1]).RootElement.GetProperty("revision").GetInt32());
    }

    [Fact(DisplayName = "syncs the visible input value when scrubbing after pasting")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-EVT-16")]
    public async Task Syncs_the_visible_input_value_when_scrubbing_after_pasting()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(10);

        await test.PasteAsync(cut, "20");
        Assert.Equal("20", test.Text);
        Assert.Equal(20, changes[^1].Value);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 2);

        Assert.Equal(22, changes[^1].Value);
        Assert.Equal("22", test.Text);
    }

    [Fact(DisplayName = "port: a read-only or disabled field ignores a scrub step")]
    [Trait("Requirement", "NUM-PTR-19")]
    public async Task A_read_only_field_ignores_a_scrub_step()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0, readOnly: true);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, 4);

        Assert.Empty(changes);
        Assert.Contains("\"readOnly\":true", test.ScrubOptions[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "clears the root scrubbing state when the scrub area unmounts mid-scrub")]
    [Trait("Requirement", "NUM-STA-15")]
    [Trait("Requirement", "NUM-PTR-17")]
    public async Task Clears_the_root_scrubbing_state_when_the_scrub_area_unmounts_mid_scrub()
    {
        bool mounted = true;
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            if (mounted)
            {
                b.OpenComponent<NumberFieldScrubArea>(1);
                b.CloseComponent();
            }
        }, value: 0);

        await test.ScrubStartAsync(cut);
        await test.ScrubStepAsync(cut, -10);
        Assert.True(cut.Find("div").HasAttribute("data-scrubbing"));

        mounted = false;
        cut.Render();

        Assert.False(cut.Find("div").HasAttribute("data-scrubbing"));
        Assert.Empty(commits);
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <NumberField.ScrubArea>")]
    [Trait("Requirement", "NUM-EDGE-02")]
    [Trait("Requirement", "NUM-STR-03")]
    public void Throws_a_descriptive_error_when_rendered_outside_the_scrub_area()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render(b =>
        {
            b.OpenComponent<NumberFieldScrubAreaCursor>(0);
            b.CloseComponent();
        }));

        Assert.Equal(
            "Iodine: `NumberFieldScrubAreaContext` is missing. NumberFieldScrubArea parts must be placed within `<NumberFieldScrubArea>`.",
            error.Message);
    }

    [Fact(DisplayName = "renders when using mouse input")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-DOM-13")]
    public async Task Renders_when_using_mouse_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);
        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));

        await test.ScrubStartAsync(cut, cursor: true);

        AngleSharp.Dom.IElement cursor = cut.Find("[data-testid=scrub-area-cursor]");
        Assert.Equal("presentation", cursor.GetAttribute("role"));
        Assert.Equal("position: fixed; top: 0; left: 0; pointer-events: none", cursor.GetAttribute("style"));
        Assert.True(cursor.ParentElement!.HasAttribute("data-base-ui-portal"));
    }

    [Fact(DisplayName = "only renders a cursor for the active scrub area")]
    [Trait("Requirement", "NUM-STR-05")]
    public async Task Only_renders_a_cursor_for_the_active_scrub_area()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render(b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            for (int i = 0; i < 2; i++)
            {
                b.OpenComponent<NumberFieldScrubArea>(1);
                b.AddComponentParameter(2, nameof(NumberFieldScrubArea.ChildContent), (RenderFragment)Cursor);
                b.CloseComponent();
            }
        }, value: 0);

        await test.ScrubStartAsync(cut, area: 0);

        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "does not render when using touch input")]
    [Trait("Requirement", "NUM-STR-06")]
    public async Task Does_not_render_when_using_touch_input()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut, cursor: false);

        Assert.NotNull(cut.Find("[data-scrubbing]"));
        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "handles pointer lock denial through requestPointerLock API")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-EDGE-09")]
    public async Task Handles_pointer_lock_denial()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubLockAsync(cut, denied: true);

        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubStepAsync(cut, 3);
        Assert.Equal("3", test.Text);
    }

    [Fact(DisplayName = "does not render after a quick tap when pointer lock resolves later")]
    [Trait("Requirement", "NUM-STR-06")]
    [Trait("Requirement", "NUM-DOM-14")]
    public async Task Does_not_render_after_a_quick_tap_when_pointer_lock_resolves_later()
    {
        IRenderedComponent<NumberFieldRoot> cut = RenderScrub(0);

        await test.ScrubStartAsync(cut);
        Assert.Single(cut.FindAll("[data-testid=scrub-area-cursor]"));
        await test.ScrubEndAsync(cut);
        await test.ScrubLockAsync(cut, denied: false);

        Assert.Empty(cut.FindAll("[data-testid=scrub-area-cursor]"));
    }

    [Fact(DisplayName = "port: a consumer's PreventPartHandler() in onpointerdown stops the scrub-start decision")]
    [Trait("Requirement", "NUM-PTR-19")]
    [Trait("Requirement", "NUM-API-25")]
    public void A_consumer_PreventPartHandler_stops_the_scrub_start_decision()
    {
        int calls = 0;
        Render(b =>
        {
            b.OpenComponent<NumberFieldScrubArea>(0);
            b.AddAttribute(1, "onpointerdown", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.PointerEventArgs>(this, e =>
            {
                calls++;
                e.PreventPartHandler();
            }));
            b.CloseComponent();
        });

        string verdict = test.ScrubTwin().Decide("scrub-start", """{"kind":"PointerEvent","type":"pointerdown","pointerType":"mouse"}""");

        Assert.Equal("stop", verdict);
        Assert.Equal(1, calls);
    }

    private static void Cursor(RenderTreeBuilder b)
    {
        b.OpenComponent<NumberFieldScrubAreaCursor>(0);
        b.AddAttribute(1, "data-testid", "scrub-area-cursor");
        b.CloseComponent();
    }

    private IRenderedComponent<NumberFieldRoot> RenderScrub(double? value, bool readOnly = false) => Render(b =>
    {
        b.OpenComponent<NumberFieldInput>(0);
        b.CloseComponent();
        b.OpenComponent<NumberFieldScrubArea>(1);
        b.AddComponentParameter(2, nameof(NumberFieldScrubArea.ChildContent), (RenderFragment)Cursor);
        b.CloseComponent();
    }, value, readOnly);

    private IRenderedComponent<NumberFieldRoot> Render(RenderFragment parts, double? value = null, bool readOnly = false) =>
        test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, value)
            .Add(x => x.ReadOnly, readOnly)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => changes.Add(e))
            .Add(x => x.OnValueCommitted, (ValueCommittedEventArgs<double?> e) => commits.Add(e))
            .Add(x => x.ChildContent, parts));
}
