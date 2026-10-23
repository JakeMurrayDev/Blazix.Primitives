using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxGroup.test.tsx and useCheckboxGroupParent.test.tsx, the rows that render neither Field nor Form (issue 82), and
// the group rows of checkbox.md (CHK-STR-04, CHK-API-05, CHK-STA-07/08/12, CHK-DOM-07/16, CHK-EVT-09, CHK-ARIA-12,
// CHK-EDGE-12). A click reaches the toggled box as the button module's report (ToggleAsync) or decision
// (DecideToggleAsync); the browser half is tests/browser/checkbox-group.spec.ts. Port titles are marked as such.
/// <summary>The checkbox group: its element, its value, its members, its parent box and the request chain.</summary>
public sealed class CheckboxGroupTests : IDisposable
{
    private static readonly string[] s_all = ["a", "b", "c"];

    private readonly CheckboxTestContext test = new();
    private readonly CapturingLogger<CheckboxGroup> logger = new();

    public CheckboxGroupTests()
    {
        test.Context.Services.AddSingleton<ILogger<CheckboxGroup>>(logger);
    }

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a member toggle renders the boxes whose state changed, once each, and not the group")]
    [Trait("Requirement", "CKG-PTR-01")]
    public async Task Port_a_member_toggle_renders_only_the_boxes_whose_state_changed()
    {
        // Design point G1 (settled by the orchestrator 2026-10-23): the listener-list context, each box comparing what it
        // renders (ADR 16 rule 9). A part calls its class function once per render of its element.
        Dictionary<string, int> renders = new() { ["group"] = 0, ["parent"] = 0, ["a"] = 0, ["b"] = 0, ["c"] = 0 };
        RenderFragment Box(string id, bool parent) => b =>
        {
            b.OpenComponent<CheckboxRoot>(0);
            b.AddAttribute(1, "data-testid", id);
            b.AddComponentParameter(2, nameof(CheckboxRoot.Parent), parent);
            b.AddComponentParameter(3, nameof(CheckboxRoot.Value), parent ? null : id);
            b.AddComponentParameter(4, nameof(CheckboxRoot.ClassValue), (Func<CheckboxRootState, string?>)(_ =>
            {
                renders[id]++;
                return null;
            }));
            b.CloseComponent();
        };
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.DefaultValue, ["a"])
            .Add(x => x.ClassValue, _ =>
            {
                renders["group"]++;
                return null;
            })
            .AddChildContent(b =>
            {
                b.AddContent(0, Box("parent", parent: true));
                b.AddContent(1, Box("a", parent: false));
                b.AddContent(2, Box("b", parent: false));
                b.AddContent(3, Box("c", parent: false));
            }));
        await cut.InvokeAsync(static () => { });
        Dictionary<string, int> before = new(renders);

        await test.ToggleAsync(cut, "b");
        await cut.InvokeAsync(static () => { });

        // The parent stays mixed.
        Assert.Equal(["group=0", "parent=0", "a=0", "b=1", "c=0"], renders.Select(entry => $"{entry.Key}={entry.Value - before[entry.Key]}"));
        before = new(renders);

        await test.ToggleAsync(cut, "c");
        await cut.InvokeAsync(static () => { });

        // The parent becomes checked.
        Assert.Equal(["group=0", "parent=1", "a=0", "b=0", "c=1"], renders.Select(entry => $"{entry.Key}={entry.Value - before[entry.Key]}"));
    }

    [Fact(DisplayName = "assigns a root id to a valueless native button in a parent checkbox group")]
    [Trait("Requirement", "CHK-DOM-01")]
    [Trait("Requirement", "CHK-EDGE-12")]
    public void Assigns_a_root_id_to_a_valueless_native_button_in_a_parent_checkbox_group()
    {
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.AllValues, ["one"])
            .AddChildContent<CheckboxRoot>(b => b
                .Add(x => x.NativeButton, true)
                .Add(x => x.Render, (RenderFragment)(r =>
                {
                    r.OpenElement(0, "button");
                    r.CloseElement();
                }))));

        Assert.False(string.IsNullOrEmpty(cut.Find("[role=checkbox]").GetAttribute("id")));
    }

    [Fact(DisplayName = "assigns an input id to a valueless child in a parent checkbox group")]
    [Trait("Requirement", "CHK-DOM-15")]
    [Trait("Requirement", "CHK-EDGE-12")]
    public void Assigns_an_input_id_to_a_valueless_child_in_a_parent_checkbox_group()
    {
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.AllValues, ["one"])
            .AddChildContent<CheckboxRoot>());

        Assert.False(string.IsNullOrEmpty(cut.Find("input[type=checkbox]").GetAttribute("id")));
    }

    [Theory(DisplayName = "wires parent aria-controls once the fallback ids are assigned (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CHK-ARIA-12")]
    [Trait("Requirement", "CHK-EDGE-10")]
    public void Wires_parent_aria_controls_once_the_fallback_ids_are_assigned(bool nativeButton)
    {
        // CheckboxRoot.react17.test.tsx, translated by its end state (checkbox §16 Q10: generated ids are drawn eagerly).
        RenderFragment Box(string testId, string? value) => b =>
        {
            b.OpenComponent<CheckboxRoot>(0);
            b.AddAttribute(1, "data-testid", testId);
            b.AddComponentParameter(2, nameof(CheckboxRoot.Parent), value is null);
            b.AddComponentParameter(3, nameof(CheckboxRoot.Value), value);
            b.AddComponentParameter(4, nameof(CheckboxRoot.NativeButton), nativeButton);
            if (nativeButton)
            {
                b.AddComponentParameter(5, nameof(CheckboxRoot.Render), (RenderFragment)(r =>
                {
                    r.OpenElement(0, "button");
                    r.CloseElement();
                }));
            }

            b.CloseComponent();
        };
        IRenderedComponent<FieldRoot> cut = Context.Render<FieldRoot>(p => p
            .Add(x => x.Name, "apple")
            .AddChildContent<CheckboxGroup>(g => g
                .Add(x => x.AllValues, ["fuji", "gala"])
                .AddChildContent(b =>
                {
                    b.AddContent(0, Box("parent", null));
                    b.AddContent(1, Box("fuji", "fuji"));
                    b.AddContent(2, Box("gala", "gala"));
                })));

        Assert.Equal(
            $"{cut.Find("[data-testid=fuji]").GetAttribute("id")} {cut.Find("[data-testid=gala]").GetAttribute("id")}",
            cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "is forwarded to the root element")]
    [Trait("Requirement", "CKG-API-06")]
    [Trait("Requirement", "CKG-DOM-01")]
    [Trait("Requirement", "CKG-STR-01")]
    [Trait("Requirement", "CKG-ARIA-01")]
    public void Is_forwarded_to_the_root_element()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p.Add(x => x.GroupAttributes, new Dictionary<string, object> { ["id"] = "group-id" }));
        IRenderedComponent<CheckboxGroupHost> none = Context.Render<CheckboxGroupHost>();

        IElement group = cut.Find("[role=group]");
        Assert.Equal("group-id", group.GetAttribute("id"));
        Assert.Equal("DIV", group.TagName);

        // CKG-API-06: the registration id is never rendered; CKG-STR-01: one element, no inputs of its own.
        Assert.False(none.Find("[role=group]").HasAttribute("id"));
        Assert.Single(none.Nodes.OfType<IElement>());
        Assert.Empty(none.FindAll("input"));
    }

    [Fact(DisplayName = "port: the group carries no name, value, tabindex, aria-checked or aria-invalid, and a consumer role wins")]
    [Trait("Requirement", "CKG-DOM-04")]
    [Trait("Requirement", "CKG-DOM-05")]
    [Trait("Requirement", "CKG-FOC-01")]
    [Trait("Requirement", "CKG-KEY-01")]
    public void Port_the_group_carries_no_control_attributes()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.GroupAttributes, new Dictionary<string, object> { ["role"] = "radiogroup", ["aria-labelledby"] = "legend" }));

        IElement group = cut.Find("[role=radiogroup]");
        foreach (string name in new[] { "name", "value", "tabindex", "aria-checked", "aria-invalid", "onkeydown" })
        {
            Assert.False(group.HasAttribute(name), name);
        }

        Assert.Equal("legend", group.GetAttribute("aria-labelledby"));
        Assert.Empty(test.ButtonModule.Invocations["attach"]);
    }

    [Fact(DisplayName = "should control the value")]
    [Trait("Requirement", "CKG-API-01")]
    [Trait("Requirement", "CKG-STA-05")]
    [Trait("Requirement", "CKG-PTR-01")]
    [Trait("Requirement", "CHK-STA-07")]
    [Trait("Requirement", "CHK-API-01")]
    [Trait("Requirement", "CHK-STR-04")]
    public async Task Should_control_the_value()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["red"])
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));

        Assert.Equal(("true", "false", "false"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));

        await test.ToggleAsync(cut, "green");
        Assert.Equal(("true", "true", "false"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));

        await test.ToggleAsync(cut, "blue");
        Assert.Equal(("true", "true", "true"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));

        await test.ToggleAsync(cut, "green");
        Assert.Equal(("true", "false", "true"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));
    }

    [Fact(DisplayName = "supports an empty string item value")]
    [Trait("Requirement", "CKG-EDGE-05")]
    [Trait("Requirement", "CKG-STA-05")]
    [Trait("Requirement", "CHK-API-06")]
    public async Task Supports_an_empty_string_item_value()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [string.Empty])
            .Add(x => x.Boxes, [new GroupBox("empty") { Value = string.Empty }, new GroupBox("other") { Value = "other" }]));

        Assert.Equal(("true", "false"), (cut.Checked("empty"), cut.Checked("other")));

        await test.ToggleAsync(cut, "empty");

        Assert.Equal("false", cut.Checked("empty"));
    }

    [Fact(DisplayName = "treats a controlled value that becomes undefined as an empty array")]
    [Trait("Requirement", "CKG-STA-04")]
    [Trait("Requirement", "CKG-EDGE-01")]
    public void Treats_a_controlled_value_that_becomes_undefined_as_an_empty_array()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["red"])
            .Add(x => x.Boxes, [new GroupBox("red") { Value = "red" }]));
        Assert.Equal("true", cut.Checked("red"));

        cut.Render(p => p.Add(x => x.Controlled, false));

        Assert.Equal("false", cut.Checked("red"));
        Assert.Contains(logger.Entries, e => e.Message.StartsWith("A component is changing the controlled value state of CheckboxGroup to be uncontrolled.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "should be called when the value changes")]
    [Trait("Requirement", "CKG-API-03")]
    [Trait("Requirement", "CKG-EVT-01")]
    [Trait("Requirement", "CKG-STA-03")]
    public async Task Should_be_called_when_the_value_changes()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));

        await test.ToggleAsync(cut, "red");
        await test.ToggleAsync(cut, "green");
        await test.ToggleAsync(cut, "blue");

        Assert.Equal(["red", "red,green", "red,green,blue"], Joined(cut.Instance.Changes));
    }

    [Fact(DisplayName = "should treat an omitted defaultValue as an empty array")]
    [Trait("Requirement", "CKG-API-02")]
    [Trait("Requirement", "CKG-STA-03")]
    public async Task Should_treat_an_omitted_defaultValue_as_an_empty_array()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));

        await test.ToggleAsync(cut, "red");
        await test.ToggleAsync(cut, "green");
        await test.ToggleAsync(cut, "red");

        Assert.Equal(["red", "red,green", "green"], Joined(cut.Instance.Changes));
    }

    [Fact(DisplayName = "does not update the group when onValueChange cancels the event")]
    [Trait("Requirement", "CKG-EVT-04")]
    [Trait("Requirement", "CKG-STA-04")]
    [Trait("Requirement", "CHK-STA-07")]
    [Trait("Requirement", "CHK-EVT-04")]
    public async Task Does_not_update_the_group_when_onValueChange_cancels_the_event()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.CancelGroup, true)
            .Add(x => x.Boxes, [new GroupBox("red") { Value = "red" }, new GroupBox("green") { Value = "green" }]));

        // WebAssembly: the decision answers stop, so the module reverts the box natively (the cancel row).
        Assert.Equal("stop", await test.DecideToggleAsync(cut, "red"));

        Assert.Equal(["red"], Joined(cut.Instance.Changes));
        Assert.Equal(("false", "false"), (cut.Checked("red"), cut.Checked("green")));
    }

    [Fact(DisplayName = "treats null as an empty array")]
    [Trait("Requirement", "CKG-EDGE-03")]
    [Trait("Requirement", "CKG-API-02")]
    public void Treats_null_as_an_empty_array()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p.Add(x => x.DefaultValue, null));

        Assert.Single(cut.FindAll("[role=group]"));
    }

    [Fact(DisplayName = "should set the initial value")]
    [Trait("Requirement", "CKG-API-02")]
    [Trait("Requirement", "CKG-STA-05")]
    public async Task Should_set_the_initial_value()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.DefaultValue, ["red"])
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));
        Assert.Equal(("true", "false", "false"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));

        await test.ToggleAsync(cut, "green");

        Assert.Equal(("true", "true", "false"), (cut.Checked("red"), cut.Checked("green"), cut.Checked("blue")));
    }

    [Fact(DisplayName = "port: omitted defaults are not shared between groups")]
    [Trait("Requirement", "CKG-STA-10")]
    public async Task Port_omitted_defaults_are_not_shared_between_groups()
    {
        // "keeps omitted defaults isolated between groups in Strict Mode", restated under ADR 06 Amendments 2026-10-18 rule 2.
        IRenderedComponent<CheckboxGroupHost> a = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a-1", "a-2"])
            .Add(x => x.Boxes, [new GroupBox("a-parent") { Parent = true }, new GroupBox("a-1") { Value = "a-1" }, new GroupBox("a-2") { Value = "a-2" }]));
        IRenderedComponent<CheckboxGroupHost> b = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["b-1", "b-2"])
            .Add(x => x.Boxes, [new GroupBox("b-parent") { Parent = true }, new GroupBox("b-1") { Value = "b-1" }, new GroupBox("b-2") { Value = "b-2" }]));

        await test.ToggleAsync(a, "a-1");
        Assert.Equal(("mixed", "false", "false", "false"), (a.Checked("a-parent"), b.Checked("b-parent"), b.Checked("b-1"), b.Checked("b-2")));

        await test.ToggleAsync(b, "b-parent");
        Assert.Equal(("true", "true", "true", "false"), (b.Checked("b-1"), b.Checked("b-2"), a.Checked("a-1"), a.Checked("a-2")));

        await test.ToggleAsync(a, "a-parent");
        Assert.Equal(("true", "true", "true"), (a.Checked("a-1"), a.Checked("a-2"), b.Checked("b-parent")));

        await test.ToggleAsync(b, "b-1");
        Assert.Equal(("mixed", "true"), (b.Checked("b-parent"), a.Checked("a-parent")));

        // The members' twins attached once each, and a repeated render attaches nothing more.
        int attaches = test.ButtonModule.Invocations["attach"].Count;
        a.Render();
        Assert.Equal(attaches, test.ButtonModule.Invocations["attach"].Count);
    }

    [Fact(DisplayName = "disables all checkboxes when `true`")]
    [Trait("Requirement", "CKG-API-05")]
    [Trait("Requirement", "CKG-PTR-03")]
    [Trait("Requirement", "CKG-DOM-02")]
    [Trait("Requirement", "CHK-API-11")]
    [Trait("Requirement", "CHK-ARIA-05")]
    public async Task Disables_all_checkboxes_when_true()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));

        Assert.All(new[] { "red", "green", "blue" }, id => Assert.Equal("true", cut.Find($"[data-testid={id}]").GetAttribute("aria-disabled")));
        Assert.Equal(string.Empty, cut.Find("[role=group]").GetAttribute("data-disabled"));

        // CKG-PTR-03: no request reaches the group.
        Assert.Equal("stop", await test.DecideToggleAsync(cut, "red"));
        Assert.Empty(cut.Instance.Changes);
    }

    [Fact(DisplayName = "does not disable all checkboxes when `false`")]
    [Trait("Requirement", "CKG-API-05")]
    [Trait("Requirement", "CHK-ARIA-05")]
    public void Does_not_disable_all_checkboxes_when_false()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Disabled, false)
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red" }, new GroupBox("green") { Name = "green" }, new GroupBox("blue") { Name = "blue" }]));

        Assert.All(new[] { "red", "green", "blue" }, id => Assert.NotEqual("true", cut.Find($"[data-testid={id}]").GetAttribute("aria-disabled")));
        Assert.False(cut.Find("[role=group]").HasAttribute("data-disabled"));
    }

    [Fact(DisplayName = "takes precedence over individual checkboxes")]
    [Trait("Requirement", "CKG-API-05")]
    [Trait("Requirement", "CHK-API-11")]
    public void Takes_precedence_over_individual_checkboxes()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.Boxes, [new GroupBox("red") { Name = "red", Disabled = false }, new GroupBox("green") { Name = "green" }]));

        Assert.Equal("true", cut.Find("[data-testid=red]").GetAttribute("aria-disabled"));
        Assert.Equal("true", cut.Find("[data-testid=green]").GetAttribute("aria-disabled"));
    }

    [Fact(DisplayName = "port: the group's state and data attributes outside a field")]
    [Trait("Requirement", "CKG-STA-01")]
    [Trait("Requirement", "CKG-DOM-02")]
    [Trait("Requirement", "CKG-DOM-03")]
    public void Port_the_groups_state_and_data_attributes_outside_a_field()
    {
        CheckboxGroupState? state = null;
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.Disabled, true)
            .Add(x => x.ClassValue, s =>
            {
                state = s;
                return null;
            }));

        Assert.Equal(new CheckboxGroupState(true, false, false, null, false, false), state);
        Assert.Equal(
            ["data-disabled", "role"],
            cut.Find("[role=group]").Attributes.Select(a => a.Name).Where(n => n != "blazor:elementreference").Order(StringComparer.Ordinal));
        Assert.Equal("data-disabled", CheckboxGroupDataAttributes.Disabled);
    }

    [Fact(DisplayName = "port: ownership is fixed at the first parameter set, and a changed defaultValue logs and changes nothing")]
    [Trait("Requirement", "CKG-STA-02")]
    [Trait("Requirement", "CKG-EDGE-02")]
    public void Port_ownership_is_fixed_at_the_first_parameter_set()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.DefaultValue, ["a"])
            .Add(x => x.Boxes, [new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }]));

        cut.Render(p => p.Add(x => x.DefaultValue, ["b"]));

        Assert.Equal(("true", "false"), (cut.Checked("a"), cut.Checked("b")));
        Assert.Contains(logger.Entries, e => e.Message.StartsWith("A component is changing the default value state of an uncontrolled CheckboxGroup after being initialized.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "should control child checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-PTR-02")]
    [Trait("Requirement", "CKG-EVT-06")]
    [Trait("Requirement", "CHK-API-05")]
    [Trait("Requirement", "CHK-DOM-07")]
    [Trait("Requirement", "CHK-EVT-05")]
    public async Task Should_control_child_checkboxes()
    {
        GroupBox parent = new("parent") { Parent = true };
        GroupBox b = new("b") { Value = "b" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [parent, new GroupBox("a") { Value = "a" }, b, new GroupBox("c") { Value = "c" }]));
        IElement[] members = [.. cut.FindAll("[role=checkbox]").Where(e => !e.HasAttribute("data-parent"))];
        Assert.Equal(3, members.Length);
        Assert.All(members, m => Assert.Equal("false", m.GetAttribute("aria-checked")));

        await test.ToggleAsync(cut, "parent");
        Assert.Equal("true", cut.Checked("parent"));
        Assert.All(new[] { "a", "b", "c" }, id => Assert.Equal("true", cut.Checked(id)));
        Assert.Single(parent.Changes);
        Assert.Empty(b.Changes);

        await test.ToggleAsync(cut, "parent");
        Assert.Equal("false", cut.Checked("parent"));
        Assert.All(new[] { "a", "b", "c" }, id => Assert.Equal("false", cut.Checked(id)));
        Assert.Equal(2, parent.Changes.Count);
        Assert.Empty(b.Changes);
    }

    [Fact(DisplayName = "parent should be marked as mixed if some children are checked")]
    [Trait("Requirement", "CKG-STA-06")]
    [Trait("Requirement", "CHK-STA-08")]
    public async Task Parent_should_be_marked_as_mixed_if_some_children_are_checked()
    {
        GroupBox a = new("a") { Value = "a" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, a, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "a");

        Assert.Single(a.Changes);
        Assert.Equal("mixed", cut.Checked("parent"));
    }

    [Fact(DisplayName = "updates uncontrolled parent-enabled groups from child clicks without duplicate callbacks")]
    [Trait("Requirement", "CKG-EVT-05")]
    [Trait("Requirement", "CKG-STA-06")]
    public async Task Updates_uncontrolled_parent_enabled_groups_from_child_clicks_without_duplicate_callbacks()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "a");
        Assert.Equal(["a"], Joined(cut.Instance.Changes));
        Assert.Equal(("mixed", "true", "false"), (cut.Checked("parent"), cut.Checked("a"), cut.Checked("b")));

        await test.ToggleAsync(cut, "parent");
        Assert.Equal(2, cut.Instance.Changes.Count);
        Assert.Equal(["a", "b", "c"], cut.Instance.Changes[1]);
        Assert.All(new[] { "parent", "a", "b", "c" }, id => Assert.Equal("true", cut.Checked(id)));

        await test.ToggleAsync(cut, "parent");
        Assert.Equal(3, cut.Instance.Changes.Count);
        Assert.Empty(cut.Instance.Changes[2]);
        Assert.All(new[] { "parent", "a", "b", "c" }, id => Assert.Equal("false", cut.Checked(id)));
    }

    [Fact(DisplayName = "should correctly initialize the values array")]
    [Trait("Requirement", "CKG-STA-06")]
    [Trait("Requirement", "CHK-STA-08")]
    public void Should_correctly_initialize_the_values_array()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["a"])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        Assert.Equal(("mixed", "true"), (cut.Checked("parent"), cut.Checked("a")));
    }

    [Fact(DisplayName = "should update the values array when a child checkbox is clicked")]
    [Trait("Requirement", "CKG-STA-06")]
    [Trait("Requirement", "CHK-STA-08")]
    public async Task Should_update_the_values_array_when_a_child_checkbox_is_clicked()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["a"])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "b");
        await test.ToggleAsync(cut, "c");

        Assert.Equal("true", cut.Checked("parent"));
    }

    [Fact(DisplayName = "should apply space-separated aria-controls attribute with child names")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    [Trait("Requirement", "CKG-API-04")]
    public void Should_apply_space_separated_aria_controls_attribute_with_child_names()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        Assert.Equal(
            string.Join(' ', s_all.Select(v => cut.Find($"[data-testid={v}]").GetAttribute("id"))),
            cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "keeps a custom child id in aria-controls")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    public void Keeps_a_custom_child_id_in_aria_controls()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true, NativeButton = true, Render = Button() }, new GroupBox("a") { Value = "a", Id = "custom", NativeButton = true, Render = Button() }]));

        Assert.Equal("custom", cut.Find("[data-testid=a]").GetAttribute("id"));
        Assert.Equal("custom", cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Theory(DisplayName = "keeps a rendered child id in aria-controls (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    public void Keeps_a_rendered_child_id_in_aria_controls(bool nativeButton)
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a"])
            .Add(x => x.Boxes,
            [
                new GroupBox("parent") { Parent = true, NativeButton = nativeButton, Render = nativeButton ? Button() : null },
                new GroupBox("a") { Value = "a", NativeButton = nativeButton, Render = Element(nativeButton ? "button" : "span", "rendered") },
            ]));

        Assert.Equal("rendered", cut.Find("[data-testid=a]").GetAttribute("id"));
        Assert.Equal("rendered", cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "references the exposed child rather than its custom-id input without nativeButton")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    public void References_the_exposed_child_rather_than_its_custom_id_input_without_nativeButton()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a", Id = "custom" }]));

        Assert.Single(cut.FindAll("input[type=checkbox][id=custom]"));
        string rootId = cut.Find("[data-testid=a]").GetAttribute("id")!;
        Assert.NotEqual("custom", rootId);
        Assert.Equal(rootId, cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "does not read aria-controls ids off Object.prototype")]
    [Trait("Requirement", "CKG-EDGE-04")]
    [Trait("Requirement", "CKG-ARIA-06")]
    public void Does_not_read_aria_controls_ids_off_Object_prototype()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a", "constructor"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }]));

        Assert.Equal(cut.Find("[data-testid=a]").GetAttribute("id"), cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "drops an unmounted child from aria-controls")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    public void Drops_an_unmounted_child_from_aria_controls()
    {
        GroupBox b = new("b") { Value = "b" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, b]));

        b.Mounted = false;
        cut.Render();

        Assert.Equal(cut.Find("[data-testid=a]").GetAttribute("id"), cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "keeps both ids for checkboxes sharing a value and retains the survivor")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-ARIA-12")]
    public void Keeps_both_ids_for_checkboxes_sharing_a_value_and_retains_the_survivor()
    {
        GroupBox second = new("second-b") { Value = "b" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, second]));
        string Id(string testId) => cut.Find($"[data-testid={testId}]").GetAttribute("id")!;
        Assert.Equal($"{Id("a")} {Id("b")} {Id("second-b")}", cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));

        second.Mounted = false;
        cut.Render();

        Assert.Equal($"{Id("a")} {Id("b")}", cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    [Fact(DisplayName = "does not select a child without an identifying value")]
    [Trait("Requirement", "CHK-STR-04")]
    [Trait("Requirement", "CHK-STA-12")]
    [Trait("Requirement", "CHK-EDGE-12")]
    [Trait("Requirement", "CKG-STR-02")]
    public async Task Does_not_select_a_child_without_an_identifying_value()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, ["a"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("no-value") { Id = "standalone" }, new GroupBox("checkbox-a") { Value = "a" }]));

        await test.ToggleAsync(cut, "parent");

        Assert.Equal(("true", "true", "false"), (cut.Checked("parent"), cut.Checked("checkbox-a"), cut.Checked("no-value")));
        Assert.Equal("standalone", cut.Find("[data-testid=no-value]").NextElementSibling!.GetAttribute("id"));
    }

    [Fact(DisplayName = "preserves initial state if mixed when parent is clicked")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-08")]
    public async Task Preserves_initial_state_if_mixed_when_parent_is_clicked()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "a");
        Assert.Equal("mixed", cut.Checked("parent"));
        await test.ToggleAsync(cut, "parent");
        Assert.All(new[] { "a", "b", "c" }, id => Assert.Equal("true", cut.Checked(id)));
        await test.ToggleAsync(cut, "parent");
        Assert.All(new[] { "a", "b", "c" }, id => Assert.Equal("false", cut.Checked(id)));
        await test.ToggleAsync(cut, "parent");

        Assert.Equal(("mixed", "true", "false", "false"), (cut.Checked("parent"), cut.Checked("a"), cut.Checked("b"), cut.Checked("c")));
    }

    [Fact(DisplayName = "lets a parent checkbox cancel a parent-enabled group change")]
    [Trait("Requirement", "CHK-EVT-09")]
    [Trait("Requirement", "CHK-EVT-04")]
    [Trait("Requirement", "CKG-EVT-01")]
    public async Task Lets_a_parent_checkbox_cancel_a_parent_enabled_group_change()
    {
        GroupBox parent = new("parent") { Parent = true, Cancel = true };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [parent, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "parent");

        Assert.Single(parent.Changes);
        Assert.Empty(cut.Instance.Changes);
        Assert.All(new[] { "parent", "a", "b", "c" }, id => Assert.Equal("false", cut.Checked(id)));
    }

    [Fact(DisplayName = "lets a child checkbox cancel a parent-enabled group change")]
    [Trait("Requirement", "CHK-EVT-09")]
    [Trait("Requirement", "CHK-EVT-04")]
    [Trait("Requirement", "CKG-EVT-01")]
    public async Task Lets_a_child_checkbox_cancel_a_parent_enabled_group_change()
    {
        GroupBox a = new("a") { Value = "a", Cancel = true };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, a, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "a");

        Assert.Single(a.Changes);
        Assert.Empty(cut.Instance.Changes);
        Assert.Equal(("false", "false"), (cut.Checked("parent"), cut.Checked("a")));
    }

    [Fact(DisplayName = "does not advance the parent toggle cycle when the group cancels a parent change")]
    [Trait("Requirement", "CKG-EVT-04")]
    [Trait("Requirement", "CKG-STA-08")]
    public async Task Does_not_advance_the_parent_toggle_cycle_when_the_group_cancels_a_parent_change()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["a"])
            .Add(x => x.CancelGroup, true)
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "parent");
        await test.ToggleAsync(cut, "parent");

        Assert.Equal(["a,b,c", "a,b,c"], Joined(cut.Instance.Changes));
    }

    [Fact(DisplayName = "does not pollute the parent snapshot when the group cancels a child change")]
    [Trait("Requirement", "CKG-EVT-04")]
    [Trait("Requirement", "CKG-STA-08")]
    public async Task Does_not_pollute_the_parent_snapshot_when_the_group_cancels_a_child_change()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, s_all)
            .Add(x => x.CancelGroup, true)
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "a");
        await test.ToggleAsync(cut, "parent");

        Assert.Equal(["b,c", string.Empty], Joined(cut.Instance.Changes));
    }

    [Fact(DisplayName = "handles unchecked disabled checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-09")]
    public async Task Handles_unchecked_disabled_checkboxes()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a", Disabled = true }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "parent");

        Assert.Equal(("mixed", "false"), (cut.Checked("parent"), cut.Checked("a")));
    }

    [Fact(DisplayName = "handles checked disabled checkboxes")]
    [Trait("Requirement", "CKG-STA-07")]
    [Trait("Requirement", "CKG-STA-09")]
    public async Task Handles_checked_disabled_checkboxes()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, ["a"])
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a", Disabled = true }, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        await test.ToggleAsync(cut, "parent");
        Assert.Equal(("true", "true"), (cut.Checked("a"), cut.Checked("b")));

        await test.ToggleAsync(cut, "parent");
        Assert.Equal(("true", "false"), (cut.Checked("a"), cut.Checked("b")));
    }

    [Fact(DisplayName = "port: a member's disabled state reaches the parent's next proposal at once")]
    [Trait("Requirement", "CKG-STA-09")]
    public async Task Port_a_members_disabled_state_reaches_the_parent_at_once()
    {
        // checkbox-group §16 Q8 and checkbox §16 Q9 (design point G4, settled by the orchestrator 2026-10-23): the entry is updated with the state.
        GroupBox a = new("a") { Value = "a" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.AllValues, s_all)
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, a, new GroupBox("b") { Value = "b" }, new GroupBox("c") { Value = "c" }]));

        a.Disabled = true;
        cut.Render();
        await test.ToggleAsync(cut, "parent");

        Assert.Equal(["b", "c"], cut.Instance.Changes[^1]);
    }

    [Fact(DisplayName = "sets grouped parent aria when manually indeterminate")]
    [Trait("Requirement", "CHK-STA-08")]
    [Trait("Requirement", "CHK-DOM-13")]
    public void Sets_grouped_parent_aria_when_manually_indeterminate()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.AllValues, ["one"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true, Indeterminate = true }, new GroupBox("one") { Value = "one" }]));

        Assert.Equal("mixed", cut.Checked("parent"));
        // The native property is the dom module's (tests/browser/checkbox-group.spec.ts); C# hands it the value.
        Assert.Contains(test.DomOptions, o => o.Contains("\"indeterminate\":true", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the request chain passes one details object from the box to the group")]
    [Trait("Requirement", "CKG-EVT-03")]
    [Trait("Requirement", "CKG-EVT-02")]
    [Trait("Requirement", "CKG-EVT-07")]
    [Trait("Requirement", "CHK-EVT-09")]
    public async Task Port_the_request_chain_passes_one_details_object()
    {
        GroupBox a = new("a") { Value = "a" };
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Boxes, [a]));

        await test.ToggleAsync(cut, "a");

        ChangeEventDetails details = Assert.Single(a.Details);
        Assert.Same(details, Assert.Single(cut.Instance.GroupDetails));
        Assert.Equal(EventReason.None, details.Reason);
        Assert.Equal("click", details.Event.Type);
        Assert.Null(details.Trigger);
        details.AllowPropagation();
        Assert.True(details.IsPropagationAllowed);
    }

    [Fact(DisplayName = "port: a member's hidden input submits its value while checked and an empty value while clear, and no unchecked value")]
    [Trait("Requirement", "CHK-DOM-16")]
    [Trait("Requirement", "CHK-API-07")]
    [Trait("Requirement", "CHK-DOM-19")]
    public async Task Port_a_members_hidden_input_value_follows_its_state()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Boxes, [new GroupBox("a") { Value = "a", Name = "n" }]));
        IElement Input() => cut.Find("[data-testid=a]").NextElementSibling!;

        Assert.Equal(("checkbox", string.Empty), (Input().GetAttribute("type"), Input().GetAttribute("value")));

        await test.ToggleAsync(cut, "a");

        Assert.Equal("a", Input().GetAttribute("value"));
        Assert.Empty(cut.FindAll("input[type=hidden]"));
    }

    [Fact(DisplayName = "port: a parent without allValues keeps its own state and leaves the group value alone")]
    [Trait("Requirement", "CKG-EDGE-09")]
    [Trait("Requirement", "CKG-STR-03")]
    [Trait("Requirement", "CHK-STA-12")]
    [Trait("Requirement", "CHK-EDGE-11")]
    public async Task Port_a_parent_without_allValues_keeps_its_own_state()
    {
        IRenderedComponent<CheckboxGroupHost> cut = Context.Render<CheckboxGroupHost>(p => p
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("a") { Value = "a" }]));

        await test.ToggleAsync(cut, "parent");

        Assert.Equal(("true", "false"), (cut.Checked("parent"), cut.Checked("a")));
        Assert.Empty(cut.Instance.Changes);
        Assert.False(cut.Find("[data-testid=parent]").HasAttribute("aria-controls"));
    }

    [Fact(DisplayName = "port: a box belongs to the nearest group only")]
    [Trait("Requirement", "CKG-EDGE-06")]
    [Trait("Requirement", "CKG-STR-02")]
    public async Task Port_a_box_belongs_to_the_nearest_group_only()
    {
        List<IReadOnlyList<string>> outer = [];
        List<IReadOnlyList<string>> inner = [];
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.OnValueChange, args => outer.Add(args.Value))
            .AddChildContent<CheckboxGroup>(g => g
                .Add(x => x.OnValueChange, args => inner.Add(args.Value))
                .AddChildContent<CheckboxRoot>(b => b.Add(x => x.Value, "x").AddUnmatched("data-testid", "x"))));

        CheckboxRoot box = cut.FindComponent<CheckboxRoot>().Instance;
        Blazix.Primitives.Interop.ButtonTwin twin = ((Microsoft.JSInterop.DotNetObjectReference<Blazix.Primitives.Interop.ButtonTwin>)test.ButtonModule.Invocations["attach"].Last(i => Equals(i.Arguments[0], box.Element)).Arguments[2]!).Value;
        await cut.InvokeAsync(() => twin.OnToggle($$"""{"checked":true,"event":{{CheckboxTestContext.InputClickJson()}}}"""));

        Assert.Equal(["x"], Joined(inner));
        Assert.Empty(outer);
    }

    [Fact(DisplayName = "port: the parent's group-derived aria-controls replaces the consumer's")]
    [Trait("Requirement", "CHK-DOM-09")]
    public void Port_the_parents_group_derived_aria_controls_replaces_the_consumers()
    {
        // checkbox §16 Q2's defined case (design point G3, settled by the orchestrator 2026-10-23): mirrored.
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .Add(x => x.AllValues, ["a"])
            .AddChildContent(builder =>
            {
                builder.OpenComponent<CheckboxRoot>(0);
                builder.AddComponentParameter(1, nameof(CheckboxRoot.Parent), true);
                builder.AddAttribute(2, "aria-controls", "consumer");
                builder.AddAttribute(3, "data-testid", "parent");
                builder.CloseComponent();
                builder.OpenComponent<CheckboxRoot>(4);
                builder.AddComponentParameter(5, nameof(CheckboxRoot.Value), "a");
                builder.AddAttribute(6, "data-testid", "a");
                builder.CloseComponent();
            }));

        Assert.Equal(cut.Find("[data-testid=a]").GetAttribute("id"), cut.Find("[data-testid=parent]").GetAttribute("aria-controls"));
    }

    private static string[] Joined(IEnumerable<IReadOnlyList<string>> changes) => [.. changes.Select(change => string.Join(',', change))];

    private static RenderFragment Button() => Element("button", null);

    private static RenderFragment Element(string tag, string? id) => builder =>
    {
        builder.OpenElement(0, tag);
        if (id is not null)
        {
            builder.AddAttribute(1, "id", id);
        }

        builder.CloseElement();
    };
}
