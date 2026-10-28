using System.Text.Json;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: Popover's popup composes the focus manager (PopoverPopup.tsx:108-122; FLT §4.8, PR 128's
/// <c>FocusManagerBehavior</c>): what the <c>focus-manager</c> handle is sent for Popover's props, and the focus targets'
/// resolvers called with the open and close interaction types (ADR 08 Amendments 2026-10-28).
/// </summary>
public sealed class PopoverFocusTests : IDisposable
{
    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the popup's focus manager takes Popover's props: not trapped, restoring to the popup, anchored at the active trigger with its guards")]
    [Trait("Requirement", "POP-FOC-01")]
    [Trait("Requirement", "POP-STA-10")]
    public async Task The_focus_manager_takes_Popovers_props()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec());

        await test.PressAsync(cut, "trigger-1", open: true);

        JsonElement options = Options();
        Assert.False(options.GetProperty("modal").GetBoolean());
        Assert.False(options.GetProperty("disabled").GetBoolean());
        Assert.True(options.GetProperty("initialFocus").GetBoolean());
        Assert.Equal("popup", options.GetProperty("restoreFocus").GetString());
        Assert.Equal("mouse", options.GetProperty("openInteractionType").GetString());
        Assert.Equal("trigger-1", options.GetProperty("domReferenceId").GetString());
        Assert.True(options.GetProperty("triggerFocusGuards").GetBoolean());
        Assert.StartsWith("base-ui-", options.GetProperty("sessionToken").GetString(), StringComparison.Ordinal);
    }

    [Theory(DisplayName = "port: the focus manager traps focus only for a modal popover with a close part")]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    [Trait("Requirement", "POP-STA-10")]
    [Trait("Requirement", "POP-FOC-02")]
    public async Task The_focus_manager_traps_focus_only_for_a_modal_popover_with_a_close_part(bool modal, bool close, bool trapped)
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = modal, CloseInside = close });

        await test.PressAsync(cut, "trigger-1", open: true);

        Assert.Equal(trapped, Options().GetProperty("modal").GetBoolean());
    }

    [Fact(DisplayName = "port: a hover-opened popover disables the focus manager")]
    [Trait("Requirement", "POP-FOC-08")]
    public async Task A_hover_opened_popover_disables_the_focus_manager()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { OpenOnHover = true });

        await test.HoverAsync(cut, "trigger-1", open: true);

        Assert.True(Options().GetProperty("disabled").GetBoolean());
    }

    [Fact(DisplayName = "port: a touch open focuses the popup itself by default")]
    [Trait("Requirement", "POP-FOC-03")]
    public async Task A_touch_open_focuses_the_popup_itself_by_default()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec());

        await test.PressAsync(cut, "trigger-1", open: true, pointerType: "touch");

        JsonElement options = Options();
        Assert.True(options.GetProperty("initialFocus").GetBoolean());
        Assert.True(options.TryGetProperty("initialFocusElement", out JsonElement element) && element.ValueKind == JsonValueKind.Number);
    }

    [Fact(DisplayName = "port: the initial focus resolver gets the open interaction type, and no target moves no focus")]
    [Trait("Requirement", "POP-API-21")]
    public async Task The_initial_focus_resolver_gets_the_open_interaction_type()
    {
        List<InteractionType> calls = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            InitialFocusResolver = type =>
            {
                calls.Add(type);
                return null;
            },
        });

        await test.PressAsync(cut, "trigger-1", open: true, pointerType: "pen");

        Assert.Contains(InteractionType.Pen, calls);
        Assert.False(Options().GetProperty("initialFocus").GetBoolean());
    }

    [Fact(DisplayName = "port: the final focus resolver gets the close interaction type and names the element focus returns to")]
    [Trait("Requirement", "POP-API-22")]
    public async Task The_final_focus_resolver_gets_the_close_interaction_type()
    {
        List<InteractionType> calls = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            FinalFocusResolver = type =>
            {
                calls.Add(type);
                return "elsewhere";
            },
        });
        await test.PressAsync(cut, "trigger-1", open: true);

        await test.SessionRequestAsync(cut, open: false, reason: "escape-key", eventJson: PopoverTestContext.Key("Escape"));

        Assert.Equal(InteractionType.Keyboard, calls[^1]);
        Assert.Equal("elsewhere", Options().GetProperty("returnFocusId").GetString());
    }

    private JsonElement Options() => JsonDocument.Parse(test.LastFocusOptions() ?? "{}").RootElement;

    private IRenderedComponent<PopupTestHost> Render(PopoverSpec spec) => test.Render((builder, _) => PopoverTree.Build(builder, spec));
}
