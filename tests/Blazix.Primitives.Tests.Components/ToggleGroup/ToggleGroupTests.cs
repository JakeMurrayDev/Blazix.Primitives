using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToggleGroup.test.tsx's titles verbatim for the [L2] rows of toggle-group.md (issue 102) and the Toggle rows they carry
// (toggle.md §13), the two grouped titles of Toggle.test.tsx, and port titles (marked as such) for the Test: none rows C#
// observes, the group's composite twin, the render counts and the key gate. Base UI's pointer clicks are Blazor's
// dispatch of the toggle's own onclick; the browser halves (keys, focus, the roving tabindex, the vetoes per mode) are
// tests/browser/toggle-group.spec.ts and toggle.spec.ts.
/// <summary>The toggle group with its toggles: the element, the value, the requests, the composite root and the grouped toggles.</summary>
public sealed class ToggleGroupTests : IDisposable
{
    private const string MissingValueMessage =
        "Blazix: A `<Toggle>` component rendered in a `<ToggleGroup>` has no explicit `Value` parameter. "
        + "This will cause issues between the Toggle Group and Toggle values. "
        + "Provide the `<Toggle>` with a `Value` parameter matching the `<ToggleGroup>` values parameter type.";

    private readonly ToggleTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "renders a `group`")]
    [Trait("Requirement", "TGG-STR-01")]
    [Trait("Requirement", "TGG-STR-03")]
    [Trait("Requirement", "TGG-ARIA-01")]
    [Trait("Requirement", "TGG-ARIA-03")]
    public void Renders_a_group()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddUnmatched("aria-label", "My Toggle Group"));

        IElement group = cut.Find("[role=group]");
        Assert.Equal("My Toggle Group", group.GetAttribute("aria-label"));
        Assert.Equal("DIV", group.TagName);
        Assert.False(group.HasAttribute("aria-labelledby"));
    }

    [Fact(DisplayName = "pressed state")]
    [Trait("Requirement", "TGG-API-02")]
    [Trait("Requirement", "TGG-STA-03")]
    [Trait("Requirement", "TGG-STA-06")]
    [Trait("Requirement", "TGG-PTR-01")]
    [Trait("Requirement", "TGL-DOM-02")]
    [Trait("Requirement", "TGL-STA-04")]
    public void Pressed_state_uncontrolled()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, false, false);

        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, true, false);
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-pressed"));

        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, false, true);
        Assert.True(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "prop: defaultValue")]
    [Trait("Requirement", "TGG-API-02")]
    [Trait("Requirement", "TGG-STA-03")]
    [Trait("Requirement", "TGG-PTR-01")]
    [Trait("Requirement", "TGL-STA-04")]
    [Trait("Requirement", "TGL-DOM-02")]
    public void Prop_defaultValue()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("two"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, false, true);
        Assert.True(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));

        ToggleTestContext.Toggles(cut)[0].Click();

        AssertPressed(cut, true, false);
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-pressed"));
        Assert.False(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "when Toggles omit value")]
    [Trait("Requirement", "TGG-STA-03")]
    [Trait("Requirement", "TGL-API-05")]
    [Trait("Requirement", "TGL-EDGE-05")]
    public void When_toggles_omit_value_uncontrolled()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles(null, "")));

        AssertPressed(cut, false, false);

        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, true, false);

        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, false, true);
    }

    [Fact(DisplayName = "should warn if Toggle value is not set and ToggleGroup value is defined")]
    [Trait("Requirement", "TGL-EDGE-01")]
    [Trait("Requirement", "TGG-EDGE-01")]
    [Trait("Requirement", "TGG-STA-07")]
    public void Should_warn_if_toggle_value_is_not_set_and_toggle_group_value_is_defined()
    {
        Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("one"))
            .AddChildContent(ToggleTestContext.Toggles(null, null)));

        Assert.Equal([MissingValueMessage], test.ToggleMessages);
    }

    [Fact(DisplayName = "pressed state")]
    [Trait("Requirement", "TGG-API-01")]
    [Trait("Requirement", "TGG-STA-06")]
    [Trait("Requirement", "TGG-ARIA-04")]
    [Trait("Requirement", "TGL-STA-04")]
    [Trait("Requirement", "TGL-ARIA-01")]
    public void Pressed_state_controlled()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Value, List("two"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, false, true);
        Assert.True(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));

        cut.Render(p => p.Add(x => x.Value, List("one")));
        AssertPressed(cut, true, false);
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-pressed"));

        cut.Render(p => p.Add(x => x.Value, List("two")));
        AssertPressed(cut, false, true);
        Assert.True(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));

        // TGG-ARIA-04: the group adds no other state attribute to its toggles.
        foreach (IElement toggle in ToggleTestContext.Toggles(cut))
        {
            Assert.False(toggle.HasAttribute("aria-checked"));
            Assert.False(toggle.HasAttribute("aria-selected"));
        }

        Assert.False(cut.Find("[role=group]").HasAttribute("aria-multiselectable"));
    }

    [Fact(DisplayName = "prop: value")]
    [Trait("Requirement", "TGG-API-01")]
    [Trait("Requirement", "TGG-STA-06")]
    [Trait("Requirement", "TGL-STA-04")]
    [Trait("Requirement", "TGL-DOM-02")]
    public void Prop_value()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Value, List("two"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, false, true);

        cut.Render(p => p.Add(x => x.Value, List("one")));

        AssertPressed(cut, true, false);
        Assert.True(ToggleTestContext.Toggles(cut)[0].HasAttribute("data-pressed"));
        Assert.False(ToggleTestContext.Toggles(cut)[1].HasAttribute("data-pressed"));
    }

    [Fact(DisplayName = "can disable the whole group")]
    [Trait("Requirement", "TGG-API-04")]
    [Trait("Requirement", "TGG-ARIA-05")]
    [Trait("Requirement", "TGL-API-04")]
    [Trait("Requirement", "TGL-ARIA-02")]
    [Trait("Requirement", "TGL-DOM-03")]
    public void Can_disable_the_whole_group()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Disabled, true)
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        foreach (IElement toggle in ToggleTestContext.Toggles(cut))
        {
            Assert.Equal("true", toggle.GetAttribute("aria-disabled"));
            Assert.True(toggle.HasAttribute("data-disabled"));
        }
    }

    [Fact(DisplayName = "can disable individual items")]
    [Trait("Requirement", "TGG-ARIA-05")]
    [Trait("Requirement", "TGL-API-04")]
    [Trait("Requirement", "TGL-ARIA-02")]
    [Trait("Requirement", "TGL-DOM-03")]
    public void Can_disable_individual_items()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<Toggle>(0);
            builder.AddComponentParameter(1, nameof(Toggle.Value), "one");
            builder.CloseComponent();
            builder.OpenComponent<Toggle>(2);
            builder.AddComponentParameter(3, nameof(Toggle.Value), "two");
            builder.AddComponentParameter(4, nameof(Toggle.Disabled), true);
            builder.CloseComponent();
        }));

        IReadOnlyList<IElement> toggles = ToggleTestContext.Toggles(cut);
        Assert.Equal("false", toggles[0].GetAttribute("aria-disabled"));
        Assert.False(toggles[0].HasAttribute("data-disabled"));
        Assert.Equal("true", toggles[1].GetAttribute("aria-disabled"));
        Assert.True(toggles[1].HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "vertical")]
    [Trait("Requirement", "TGG-API-05")]
    [Trait("Requirement", "TGG-DOM-02")]
    public void Vertical()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        Assert.Equal("vertical", cut.Find("[role=group]").GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "does not render aria-orientation on role=\"group\"")]
    [Trait("Requirement", "TGG-ARIA-02")]
    [Trait("Requirement", "TGG-DOM-02")]
    public void Does_not_render_aria_orientation_on_role_group()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Horizontal)
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        IElement group = cut.Find("[role=group]");
        Assert.Equal("horizontal", group.GetAttribute("data-orientation"));
        Assert.False(group.HasAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "sets data-multiple only when true")]
    [Trait("Requirement", "TGG-DOM-04")]
    [Trait("Requirement", "TGG-API-07")]
    public void Sets_data_multiple_only_when_true()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one")));

        Assert.False(cut.Find("[role=group]").HasAttribute("data-multiple"));

        cut.Render(p => p.Add(x => x.Multiple, true));
        Assert.True(cut.Find("[role=group]").HasAttribute("data-multiple"));

        cut.Render(p => p.Add(x => x.Multiple, false));
        Assert.False(cut.Find("[role=group]").HasAttribute("data-multiple"));
    }

    [Fact(DisplayName = "port: changing Multiple keeps the value, and the next activation follows the new rule")]
    [Trait("Requirement", "TGG-STA-05")]
    [Trait("Requirement", "TGG-DOM-04")]
    public void Port_changing_multiple_keeps_the_value()
    {
        // ToggleGroup.test.tsx "preserves selection and roving focus when standalone", its value and data-multiple half
        // (the roving half is toggle-group.spec.ts's).
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("one"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, false, true);

        cut.Render(p => p.Add(x => x.Multiple, true));
        Assert.True(cut.Find("[role=group]").HasAttribute("data-multiple"));
        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, true, true);

        // No normalization: two pressed toggles stay pressed when the group becomes single-select.
        cut.Render(p => p.Add(x => x.Multiple, false));
        Assert.False(cut.Find("[role=group]").HasAttribute("data-multiple"));
        AssertPressed(cut, true, true);

        cut.Render(p => p.Add(x => x.Multiple, true));
        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, true, false);

        cut.Render(p => p.Add(x => x.Multiple, false));
        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, false, true);
    }

    [Fact(DisplayName = "multiple items can be pressed when true")]
    [Trait("Requirement", "TGG-API-07")]
    [Trait("Requirement", "TGG-STA-04")]
    public void Multiple_items_can_be_pressed_when_true()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Multiple, true)
            .Add(x => x.DefaultValue, List("one"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, true, false);

        ToggleTestContext.Toggles(cut)[1].Click();

        AssertPressed(cut, true, true);
    }

    [Fact(DisplayName = "only one item can be pressed when false")]
    [Trait("Requirement", "TGG-API-07")]
    [Trait("Requirement", "TGG-STA-03")]
    public void Only_one_item_can_be_pressed_when_false()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("one"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        AssertPressed(cut, true, false);

        ToggleTestContext.Toggles(cut)[1].Click();

        AssertPressed(cut, false, true);
    }

    [Fact(DisplayName = "when Toggles omit value")]
    [Trait("Requirement", "TGG-STA-04")]
    [Trait("Requirement", "TGL-API-05")]
    [Trait("Requirement", "TGL-EDGE-05")]
    public void When_toggles_omit_value_multiple()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Multiple, true)
            .AddChildContent(ToggleTestContext.Toggles("", null)));

        AssertPressed(cut, false, false);

        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, true, false);

        ToggleTestContext.Toggles(cut)[1].Click();
        AssertPressed(cut, true, true);

        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, false, true);
    }

    [Fact(DisplayName = "fires when an Item is clicked")]
    [Trait("Requirement", "TGG-API-03")]
    [Trait("Requirement", "TGG-EVT-01")]
    [Trait("Requirement", "TGG-EVT-06")]
    [Trait("Requirement", "TGG-EVT-07")]
    public void Fires_when_an_item_is_clicked()
    {
        List<IReadOnlyList<string>> calls = [];
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.OnValueChange, args => calls.Add(args.Value))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        Assert.Empty(calls);

        ToggleTestContext.Toggles(cut)[0].Click();
        Assert.Single(calls);
        Assert.Equal(["one"], calls[0]);

        ToggleTestContext.Toggles(cut)[1].Click();
        Assert.Equal(2, calls.Count);
        Assert.Equal(["two"], calls[1]);
    }

    [Fact(DisplayName = "does not change the value when the event is canceled")]
    [Trait("Requirement", "TGG-EVT-03")]
    [Trait("Requirement", "TGL-EVT-05")]
    public void Does_not_change_the_value_when_the_event_is_canceled()
    {
        int calls = 0;
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.OnValueChange, args =>
            {
                calls++;
                args.Details.Cancel();
            })
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        ToggleTestContext.Toggles(cut)[0].Click();

        Assert.Equal(1, calls);
        Assert.Equal("false", ToggleTestContext.Toggles(cut)[0].GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "canceling in a grouped Toggle prevents the group value from changing")]
    [Trait("Requirement", "TGL-EVT-04")]
    [Trait("Requirement", "TGL-PTR-01")]
    [Trait("Requirement", "TGG-EVT-04")]
    public void Canceling_in_a_grouped_toggle_prevents_the_group_value_from_changing()
    {
        int groupCalls = 0;
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.OnValueChange, _ => groupCalls++)
            .AddChildContent(builder =>
            {
                builder.OpenComponent<Toggle>(0);
                builder.AddComponentParameter(1, nameof(Toggle.Value), "one");
                builder.AddComponentParameter(2, nameof(Toggle.OnPressedChange), EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args => args.Details.Cancel()));
                builder.CloseComponent();
                builder.OpenComponent<Toggle>(3);
                builder.AddComponentParameter(4, nameof(Toggle.Value), "two");
                builder.CloseComponent();
            }));

        ToggleTestContext.Toggles(cut)[0].Click();

        Assert.Equal("false", ToggleTestContext.Toggles(cut)[0].GetAttribute("aria-pressed"));
        Assert.Equal(0, groupCalls);
    }

    [Fact(DisplayName = "port: should pass composite props (the toggle registers its id with the group's root, and the render function receives its state)")]
    [Trait("Requirement", "TGL-STR-02")]
    [Trait("Requirement", "TGL-STA-01")]
    [Trait("Requirement", "TGG-STR-02")]
    public void Port_should_pass_composite_props()
    {
        List<RenderProps<ToggleState>> calls = [];
        RenderFragment<RenderProps<ToggleState>> callback = props => builder =>
        {
            calls.Add(props);
            builder.OpenElement(0, "button");
            builder.AddAttribute(1, "type", "button");
            builder.AddMultipleAttributes(2, props.Attributes);
            builder.CloseElement();
        };
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("left"))
            .AddChildContent<Toggle>(t => t.Add(x => x.Value, "left").Add(x => x.RenderFunction, callback)));

        string id = cut.Find("button").GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, id, StringComparison.Ordinal);
        Assert.Equal([id], ToggleTestContext.ItemIds(test.LastCompositeOptions));

        RenderProps<ToggleState> last = calls[^1];
        Assert.Equal(new ToggleState(Pressed: true, Disabled: false), last.State);

        // The composite owns the tab stop from attach (ADR 04 Amendments 2026-10-22): C# renders -1, and the module
        // writes 0 on the item that holds it, which toggle-group.spec.ts reads.
        Assert.Equal("-1", last.Attributes["tabindex"]);
    }

    [Fact(DisplayName = "port: the group's composite root sends its orientation, loopFocus and Home and End, and no disabled indices")]
    [Trait("Requirement", "TGG-STR-02")]
    [Trait("Requirement", "TGG-API-05")]
    [Trait("Requirement", "TGG-API-06")]
    [Trait("Requirement", "TGG-KEY-04")]
    [Trait("Requirement", "TGG-KEY-06")]
    [Trait("Requirement", "TGG-KEY-07")]
    [Trait("Requirement", "TGG-KEY-08")]
    [Trait("Requirement", "TGG-PTR-03")]
    public void Port_the_groups_composite_root_sends_its_options()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one", "two")));

        JsonElement attach = JsonDocument.Parse(Assert.Single(test.CompositeAttaches)).RootElement;
        Assert.Equal("horizontal", attach.GetProperty("orientation").GetString());
        Assert.True(attach.GetProperty("loopFocus").GetBoolean());
        Assert.True(attach.GetProperty("enableHomeAndEndKeys").GetBoolean());
        Assert.True(attach.GetProperty("stopEventPropagation").GetBoolean());
        Assert.False(attach.GetProperty("highlightItemOnHover").GetBoolean());
        Assert.Equal(0, attach.GetProperty("modifierKeys").GetArrayLength());
        Assert.True(!attach.TryGetProperty("disabledIndices", out JsonElement indices) || indices.ValueKind == JsonValueKind.Null);
        Assert.False(attach.GetProperty("selectOnNavigation").GetBoolean());
        Assert.Equal(2, attach.GetProperty("items").GetArrayLength());

        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical).Add(x => x.LoopFocus, false));

        JsonElement update = JsonDocument.Parse(Assert.Single(test.CompositeUpdates)).RootElement;
        Assert.Equal("vertical", update.GetProperty("orientation").GetString());
        Assert.False(update.GetProperty("loopFocus").GetBoolean());
    }

    [Fact(DisplayName = "port: grouped toggles render tabindex -1, which the composite owns from attach, and the group renders none")]
    [Trait("Requirement", "TGL-EDGE-08")]
    [Trait("Requirement", "TGG-EDGE-09")]
    [Trait("Requirement", "TGL-DOM-05")]
    [Trait("Requirement", "TGG-DOM-05")]
    public void Port_grouped_toggles_render_tabindex_minus_one_and_the_group_renders_none()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("two"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        Assert.All(ToggleTestContext.Toggles(cut), toggle => Assert.Equal("-1", toggle.GetAttribute("tabindex")));
        IElement group = cut.Find("[role=group]");
        Assert.False(group.HasAttribute("tabindex"));
        Assert.Equal("horizontal", group.GetAttribute("data-orientation"));

        // Every later render keeps C#'s value, so Blazor never writes the attribute the module owns.
        ToggleTestContext.Toggles(cut)[0].Click();
        Assert.All(ToggleTestContext.Toggles(cut), toggle => Assert.Equal("-1", toggle.GetAttribute("tabindex")));
    }

    [Fact(DisplayName = "port: a grouped toggle's button twin is composite, and the toggle registers with the group's root")]
    [Trait("Requirement", "TGL-STR-02")]
    [Trait("Requirement", "TGG-ARIA-05")]
    [Trait("Requirement", "TGL-KEY-02")]
    public void Port_a_grouped_toggles_button_twin_is_composite_and_the_toggle_registers()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles("one", "two")));

        Assert.Equal(2, test.ButtonAttaches.Count);
        Assert.All(test.ButtonAttaches, options => Assert.Contains("\"composite\":true", options, StringComparison.Ordinal));
        string[] ids = [.. ToggleTestContext.Toggles(cut).Select(toggle => toggle.GetAttribute("id")!)];
        Assert.Equal(ids, ToggleTestContext.ItemIds(test.LastCompositeOptions));
    }

    [Fact(DisplayName = "port: a toggle's identity is its Value, else its generated id, which an empty Value does not replace")]
    [Trait("Requirement", "TGL-API-05")]
    [Trait("Requirement", "TGL-EDGE-05")]
    public void Port_a_toggles_identity_is_its_value_else_its_generated_id()
    {
        List<IReadOnlyList<string>> calls = [];
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Multiple, true)
            .Add(x => x.OnValueChange, args => calls.Add(args.Value))
            .AddChildContent(ToggleTestContext.Toggles("bold", null, "")));

        IReadOnlyList<IElement> toggles = ToggleTestContext.Toggles(cut);
        toggles[0].Click();
        ToggleTestContext.Toggles(cut)[1].Click();
        ToggleTestContext.Toggles(cut)[2].Click();

        string second = toggles[1].GetAttribute("id")!;
        string third = toggles[2].GetAttribute("id")!;
        Assert.StartsWith(PartIdGenerator.Prefix, second, StringComparison.Ordinal);
        Assert.NotEqual(second, third);
        Assert.Equal(["bold", second, third], calls[^1]);
        Assert.False(toggles[0].HasAttribute("value"));
    }

    [Fact(DisplayName = "port: a selection renders only the toggles whose pressed state changed, and not the group")]
    [Trait("Requirement", "TGG-STA-03")]
    [Trait("Requirement", "TGL-STA-04")]
    public void Port_a_selection_renders_only_the_toggles_whose_pressed_state_changed()
    {
        // Design point 11.1 (a), settled by the orchestrator 2026-10-26: the listener-list context, each toggle comparing what it renders. A part
        // calls its class function once per render of its element (the CheckboxGroup precedent).
        Dictionary<string, int> renders = new() { ["group"] = 0, ["one"] = 0, ["two"] = 0, ["three"] = 0 };
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("one"))
            .Add(x => x.ClassValue, _ =>
            {
                renders["group"]++;
                return null;
            })
            .AddChildContent(builder =>
            {
                foreach (string value in new[] { "one", "two", "three" })
                {
                    builder.OpenComponent<Toggle>(0);
                    builder.SetKey(value);
                    builder.AddComponentParameter(1, nameof(Toggle.Value), value);
                    builder.AddComponentParameter(2, nameof(Toggle.ClassValue), (Func<ToggleState, string?>)(_ =>
                    {
                        renders[value]++;
                        return null;
                    }));
                    builder.CloseComponent();
                }
            }));
        Dictionary<string, int> before = new(renders);

        ToggleTestContext.Toggles(cut)[1].Click();

        Assert.Equal(before["group"], renders["group"]);
        Assert.Equal(before["one"] + 1, renders["one"]);
        Assert.Equal(before["two"] + 1, renders["two"]);
        Assert.Equal(before["three"], renders["three"]);
    }

    [Fact(DisplayName = "port: the state is the effective disabled state, Multiple and Orientation")]
    [Trait("Requirement", "TGG-STA-01")]
    [Trait("Requirement", "TGG-API-08")]
    public void Port_the_state_is_the_effective_disabled_state_multiple_and_orientation()
    {
        List<ToggleGroupState> states = [];
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Multiple, true)
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return "group";
            }));

        Assert.Equal(new ToggleGroupState(Disabled: true, Multiple: true, Orientation: LibraryOrientation.Vertical), states[^1]);
        IElement group = cut.Find("[role=group]");
        Assert.True(group.HasAttribute("data-disabled"));
        Assert.True(group.HasAttribute("data-multiple"));
        Assert.Equal("vertical", group.GetAttribute("data-orientation"));
    }

    [Fact(DisplayName = "port: ownership is fixed at the first parameter set, and a removed Value falls back to the initial default")]
    [Trait("Requirement", "TGG-STA-02")]
    [Trait("Requirement", "TGG-EDGE-02")]
    public void Port_ownership_is_fixed_at_the_first_parameter_set()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Value, List("one"))
            .Add(x => x.DefaultValue, List("two"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));
        AssertPressed(cut, true, false);

        cut.Render(p => p.Add(x => x.DefaultValue, List("two")));
        AssertPressed(cut, false, true);

        // Still consumer-owned: a click does not commit.
        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, false, true);
        Assert.Contains(test.GroupMessages, message => message.StartsWith(
            "A component is changing the controlled value state of ToggleGroup to be uncontrolled.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: a DefaultValue whose entries change after the first parameter set is logged, and a new list with the same entries is not")]
    [Trait("Requirement", "TGG-EDGE-03")]
    public void Port_a_default_value_whose_entries_change_is_logged()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.DefaultValue, List("one"))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        cut.Render(p => p.Add(x => x.DefaultValue, List("one")));
        Assert.Empty(test.GroupMessages);

        cut.Render(p => p.Add(x => x.DefaultValue, List("two")));
        AssertPressed(cut, true, false);
        Assert.Equal(
            ["A component is changing the default value state of an uncontrolled ToggleGroup after being initialized. To suppress this warning opt to use a controlled ToggleGroup."],
            test.GroupMessages);
    }

    [Fact(DisplayName = "port: the group's request carries the toggle's details instance, with the reason none and the click")]
    [Trait("Requirement", "TGG-EVT-02")]
    [Trait("Requirement", "TGG-EVT-09")]
    [Trait("Requirement", "TGL-EVT-05")]
    public void Port_the_groups_request_carries_the_toggles_details_instance()
    {
        ChangeEventDetails? toggleDetails = null;
        ChangeEventDetails? groupDetails = null;
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.OnValueChange, args =>
            {
                groupDetails = args.Details;
                args.Details.AllowPropagation();
            })
            .AddChildContent<Toggle>(t => t
                .Add(x => x.Value, "one")
                .Add(x => x.OnPressedChange, args => toggleDetails = args.Details)));

        cut.Find("button").Click(new MouseEventArgs { Type = "click", AltKey = true, Detail = 1 });

        Assert.NotNull(groupDetails);
        Assert.Same(toggleDetails, groupDetails);
        Assert.Equal(EventReason.None, groupDetails.Reason);
        Assert.Equal("click", groupDetails.Event.Type);
        Assert.True(groupDetails.Event.AltKey);
        Assert.Null(groupDetails.Trigger);
        Assert.Equal("true", cut.Find("button").GetAttribute("aria-pressed"));
    }

    [Fact(DisplayName = "port: a consumer-owned group's click raises OnValueChange and ValueChanged and shows the consumer's next value")]
    [Trait("Requirement", "TGG-STA-06")]
    [Trait("Requirement", "TGG-EVT-10")]
    [Trait("Requirement", "TGG-EVT-07")]
    public void Port_a_consumer_owned_groups_click_raises_the_callbacks()
    {
        List<string> order = [];
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Value, List("one"))
            .Add(x => x.OnValueChange, args => order.Add("change:" + string.Join(',', args.Value)))
            .Add(x => x.ValueChanged, value => order.Add("bind:" + string.Join(',', value)))
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        ToggleTestContext.Toggles(cut)[1].Click();

        Assert.Equal(["change:two", "bind:two"], order);
        AssertPressed(cut, true, false);

        cut.Render(p => p.Add(x => x.Value, List("two")));
        AssertPressed(cut, false, true);
        Assert.Equal(2, order.Count);
    }

    [Fact(DisplayName = "port: a disabled group's toggles ignore clicks")]
    [Trait("Requirement", "TGG-PTR-02")]
    [Trait("Requirement", "TGG-EVT-08")]
    [Trait("Requirement", "TGL-EVT-08")]
    public void Port_a_disabled_groups_toggles_ignore_clicks()
    {
        int calls = 0;
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.OnValueChange, _ => calls++)
            .AddChildContent(ToggleTestContext.Toggles("one", "two")));

        // The module prevents a disabled click in a browser; a dispatch that still arrives changes nothing.
        ToggleTestContext.Toggles(cut)[0].Click();

        Assert.Equal(0, calls);
        AssertPressed(cut, false, false);
        Assert.All(ToggleTestContext.Toggles(cut), toggle => Assert.True(toggle.HasAttribute("disabled")));
    }

    [Fact(DisplayName = "port: a grouped toggle ignores Pressed and DefaultPressed and logs nothing")]
    [Trait("Requirement", "TGL-EDGE-06")]
    [Trait("Requirement", "TGL-STA-04")]
    public void Port_a_grouped_toggle_ignores_pressed_and_default_pressed()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(builder =>
        {
            builder.OpenComponent<Toggle>(0);
            builder.AddComponentParameter(1, nameof(Toggle.Value), "one");
            builder.AddComponentParameter(2, nameof(Toggle.Pressed), true);
            builder.CloseComponent();
            builder.OpenComponent<Toggle>(3);
            builder.AddComponentParameter(4, nameof(Toggle.Value), "two");
            builder.AddComponentParameter(5, nameof(Toggle.DefaultPressed), true);
            builder.CloseComponent();
        }));

        AssertPressed(cut, false, false);

        ToggleTestContext.Toggles(cut)[0].Click();
        AssertPressed(cut, true, false);
        Assert.Empty(test.ToggleMessages);
    }

    [Fact(DisplayName = "port: no missing-value message without an initialized value, or for an empty Value")]
    [Trait("Requirement", "TGL-EDGE-01")]
    [Trait("Requirement", "TGG-STA-07")]
    public void Port_no_missing_value_message_without_an_initialized_value()
    {
        Context.Render<ToggleGroup>(p => p.AddChildContent(ToggleTestContext.Toggles(null, null)));
        Context.Render<ToggleGroup>(p => p.Add(x => x.Value, (IReadOnlyList<string>?)null).Add(x => x.DefaultValue, (IReadOnlyList<string>?)null).AddChildContent(ToggleTestContext.Toggles((string?)null)));
        Context.Render<ToggleGroup>(p => p.Add(x => x.DefaultValue, List("one")).AddChildContent(ToggleTestContext.Toggles("")));

        Assert.Empty(test.ToggleMessages);
    }

    [Fact(DisplayName = "port: the missing-value message is written once a later parameter set initializes the value")]
    [Trait("Requirement", "TGL-EDGE-01")]
    [Trait("Requirement", "TGG-STA-07")]
    public void Port_the_missing_value_message_is_written_once_the_value_is_initialized()
    {
        RenderFragment toggles = ToggleTestContext.Toggles(null, null);
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent(toggles));
        Assert.Empty(test.ToggleMessages);

        cut.Render(p => p.Add(x => x.DefaultValue, List("one")).AddChildContent(toggles));

        Assert.Equal([MissingValueMessage], test.ToggleMessages);
    }

    [Fact(DisplayName = "port: in a group, arrows, Home and End run the toggle's consumer onkeydown through the item gate, once")]
    [Trait("Requirement", "TGL-KEY-03")]
    [Trait("Requirement", "TGG-KEY-04")]
    [Trait("Requirement", "TGG-API-09")]
    public async Task Port_in_a_group_arrows_home_and_end_run_the_consumer_onkeydown_once()
    {
        // Design point 12.3 (a), settled by the orchestrator 2026-10-26: Home and End take the item gate as the arrows do.
        List<string> keys = [];
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            keys.Add(e.Key);
            e.PreventPartHandler();
        });
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.AddChildContent<Toggle>(t => t
            .Add(x => x.Value, "one")
            .AddUnmatched("onkeydown", onKeyDown)));
        string id = cut.Find("button").GetAttribute("id")!;

        // WebAssembly: `navigate` runs the handler inside the dispatch; its veto leaves the key to Blazor, which dispatches it.
        foreach (string key in new[] { "ArrowRight", "Home", "End" })
        {
            Assert.Equal("stop", await test.DecideNavigateAsync(cut, key, id));
            cut.Find("button").KeyDown(new KeyboardEventArgs { Key = key, Code = key });
        }

        Assert.Equal(["ArrowRight", "Home", "End"], keys);

        // Server: the module handled and stopped the key, and reports it; the handler runs once, a round trip late.
        await test.ReportHandledKeyAsync(cut, "End", id);
        Assert.Equal(["ArrowRight", "Home", "End", "End"], keys);
    }

    [Fact(DisplayName = "port: the group declines its consumer onkeydown, which the root runs at navigate")]
    [Trait("Requirement", "TGG-API-09")]
    [Trait("Requirement", "TGG-DOM-06")]
    public async Task Port_the_group_declines_its_consumer_onkeydown_which_the_root_runs_at_navigate()
    {
        List<string> keys = [];
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
        {
            keys.Add(e.Key);
            e.PreventPartHandler();
        });
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p
            .AddUnmatched("onkeydown", onKeyDown)
            .AddChildContent(ToggleTestContext.Toggles("one")));
        string id = cut.Find("button").GetAttribute("id")!;

        Assert.Equal("stop", await test.DecideNavigateAsync(cut, "ArrowRight", id));
        cut.Find("[role=group]").KeyDown(new KeyboardEventArgs { Key = "ArrowRight", Code = "ArrowRight" });

        Assert.Equal(["ArrowRight"], keys);
        Assert.Contains("\"reportHandledKeys\":true", test.LastCompositeOptions, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the data attribute constants are Base UI's")]
    [Trait("Requirement", "TGG-DOM-02")]
    [Trait("Requirement", "TGG-DOM-03")]
    [Trait("Requirement", "TGG-DOM-04")]
    public void Port_the_data_attribute_constants_are_base_uis()
    {
        IRenderedComponent<ToggleGroup> cut = Context.Render<ToggleGroup>(p => p.Add(x => x.Disabled, true).Add(x => x.Multiple, true));

        Assert.Equal("data-disabled", ToggleGroupDataAttributes.Disabled);
        Assert.Equal("data-orientation", ToggleGroupDataAttributes.Orientation);
        Assert.Equal("data-multiple", ToggleGroupDataAttributes.Multiple);
        IElement group = cut.Find("[role=group]");
        Assert.Equal(string.Empty, group.GetAttribute(ToggleGroupDataAttributes.Disabled));
        Assert.Equal("horizontal", group.GetAttribute(ToggleGroupDataAttributes.Orientation));
        Assert.Equal(string.Empty, group.GetAttribute(ToggleGroupDataAttributes.Multiple));
    }

    private static IReadOnlyList<string> List(params string[] values) => values;

    private static void AssertPressed(IRenderedComponent<ToggleGroup> cut, params bool[] pressed)
    {
        Assert.Equal(pressed.Select(value => value ? "true" : "false"), ToggleTestContext.Toggles(cut).Select(toggle => toggle.GetAttribute("aria-pressed")));
    }
}
