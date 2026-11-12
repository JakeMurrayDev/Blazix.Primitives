using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.3 at L2 (ComboboxInputGroup.test.tsx's titles whose assertions are what C# renders, sends and routes): the role, the
// state attributes, the dismissal exemption, and the input press the `combobox` module reports (its focus move and its
// target checks are the module's, L3).
/// <summary>Combobox's InputGroup at L2.</summary>
public sealed class ComboboxInputGroupTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // ADR 04 Amendments 2026-11-10: the press asks `press` while C# holds a consumer onmousedown; C# runs it inside the
    // dispatch, and Blazor's later dispatch of that mousedown skips it (CMB-PTR-08's preventBaseUIHandler clause).
    [Fact(DisplayName = "port: the input group's onmousedown runs once inside press, and its PreventPartHandler() vetoes the press")]
    [Trait("Requirement", "CMB-PTR-08")]
    public async Task The_input_groups_onmousedown_runs_once_inside_press()
    {
        int calls = 0;
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInputGroup>(0);
            builder.AddComponentParameter(1, "data-testid", "group");
            builder.AddComponentParameter(2, "onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, e =>
            {
                calls++;
                e.PreventPartHandler();
            }));
            builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<ComboboxInput>(0);
                inner.AddComponentParameter(1, "data-testid", "input");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p.Add(x => x.WithInput, false).Add(x => x.Extra, (RenderFragment)Extra));
        Assert.Contains("\"decidesPress\":true", test.LastOptions(ComboboxTwin.InputGroupPart), StringComparison.Ordinal);
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputGroupPart);

        Assert.Equal("stop", await cut.InvokeAsync(() => twin.Decide(ComboboxTwin.PressPoint, ComboboxTestContext.Event(ComboboxTestContext.Click, "mousedown", 2))));
        await cut.InvokeAsync(() => cut.Find("[data-testid=group]").MouseDown(new MouseEventArgs { Type = "mousedown", Detail = 1 }));
        Assert.Equal(1, calls);
        await cut.InvokeAsync(() => cut.Find("[data-testid=group]").MouseDown(new MouseEventArgs { Type = "mousedown", Detail = 1 }));

        Assert.Equal(2, calls);
    }

    [Fact(DisplayName = "has role prop")]
    [Trait("Requirement", "CMB-DOM-03")]
    public void Has_role_group()
    {
        IElement group = Render(new()).Find("[data-testid=group]");

        Assert.Equal("div", group.TagName.ToLowerInvariant());
        Assert.Equal("group", group.GetAttribute("role"));
    }

    [Fact(DisplayName = "port: the input group projects the open, readonly and placeholder state")]
    [Trait("Requirement", "CMB-STA-02")]
    [Trait("Requirement", "CMB-DOM-03")]
    public void Projects_the_open_readonly_and_placeholder_state()
    {
        IElement group = Render(new() { ["DefaultOpen"] = true, ["ReadOnly"] = true }).Find("[data-testid=group]");

        Assert.True(group.HasAttribute("data-popup-open"));
        Assert.True(group.HasAttribute("data-pressed"));
        Assert.True(group.HasAttribute("data-readonly"));
        Assert.True(group.HasAttribute("data-placeholder"));
    }

    [Fact(DisplayName = "should not dismiss the popup when clicking inside the input group")]
    [Trait("Requirement", "CMB-DIS-01")]
    public void Joins_the_outside_press_exemptions()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultOpen"] = true }, groupId: "the-group");

        Assert.Equal("the-group", cut.Find("[data-testid=group]").Id);
        Assert.Contains(SessionOptions(), options => options.Contains("\"outsidePressExemptIds\":[", StringComparison.Ordinal) && options.Contains("\"the-group\"", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "focuses the input and opens when clicking input-group padding around chips")]
    [Trait("Requirement", "CMB-PTR-08")]
    public async Task Opens_on_an_input_press()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new());

        await cut.InvokeAsync(() => test.TwinOf(ComboboxTwin.InputGroupPart).OnInputPress(ComboboxTestContext.Click));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.True(open);
        Assert.Equal(EventReason.InputPress, reason);
    }

    [Fact(DisplayName = "focuses the input without opening when openOnInputClick is false")]
    [Trait("Requirement", "CMB-PTR-08")]
    [Trait("Requirement", "CMB-API-20")]
    public async Task Does_not_open_without_openOnInputClick()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["OpenOnInputClick"] = false });
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputGroupPart);

        await cut.InvokeAsync(() => twin.OnInputPress(ComboboxTestContext.Click));

        Assert.Empty(cut.Instance.OpenChanges);
    }

    [Fact(DisplayName = "focuses the input and opens when readOnly")]
    [Trait("Requirement", "CMB-PTR-08")]
    [Trait("Requirement", "CMB-API-29")]
    public async Task Opens_on_an_input_press_when_readOnly()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ReadOnly"] = true });

        await cut.InvokeAsync(() => test.TwinOf(ComboboxTwin.InputGroupPart).OnInputPress(ComboboxTestContext.Click));

        (bool open, EventReason reason) = Assert.Single(cut.Instance.OpenChanges);
        Assert.True(open);
        Assert.Equal(EventReason.InputPress, reason);
    }

    [Fact(DisplayName = "port: a disabled root sends the group's handle disabled, so a press neither focuses nor opens")]
    [Trait("Requirement", "CMB-PTR-08")]
    [Trait("Requirement", "CMB-API-30")]
    public void A_disabled_root_sends_the_handle_disabled()
    {
        IElement group = Render(new() { ["Disabled"] = true }).Find("[data-testid=group]");

        Assert.True(group.HasAttribute("data-disabled"));
        string options = (string)test.Combobox.Invocations["attach"].Last(attach => attach.Arguments[1] is string json && json.Contains("\"part\":\"input-group\"", StringComparison.Ordinal)).Arguments[1]!;
        Assert.Contains("\"disabled\":true", options, StringComparison.Ordinal);
    }

    private List<string> SessionOptions()
    {
        List<string> options = [];
        foreach (JSRuntimeInvocation attach in test.Interactions.Invocations["attach"])
        {
            if (attach.Arguments[0] is null && attach.Arguments[1] is string json)
            {
                options.Add(json);
            }
        }

        foreach (JSRuntimeInvocation update in test.InteractionsHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json)
            {
                options.Add(json);
            }
        }

        return options;
    }

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, string? groupId = null)
    {
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenComponent<ComboboxInputGroup>(0);
            builder.AddComponentParameter(1, "data-testid", "group");
            if (groupId is not null)
            {
                builder.AddComponentParameter(2, "id", groupId);
            }

            builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(inner =>
            {
                inner.OpenComponent<ComboboxInput>(0);
                inner.AddComponentParameter(1, "data-testid", "input");
                inner.CloseComponent();
            }));
            builder.CloseComponent();
        }

        return test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.WithInput, false)
            .Add(x => x.Extra, (RenderFragment)Extra));
    }
}
