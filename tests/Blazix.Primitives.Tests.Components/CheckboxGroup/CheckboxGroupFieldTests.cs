using Blazix.Primitives.Interop;
using System.Linq.Expressions;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using static Blazix.Primitives.Tests.Components.CheckboxGroupFieldHost;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxGroup.test.tsx's Field, Field.Label and Field.Description titles that render no Form (issue 82), and the
// checkbox.md rows a group finishes (CHK-STA-10's group title, CHK-FRM-19..21, CHK-DOM-15, CHK-ARIA-07/10/11). The group
// is its field's control (checkbox-group §8); the members' hidden inputs are the field's validation inputs, read through
// the form module's refresh (SettleFormAsync answers the latest). Rows that need Form are open (the PR lists them).
/// <summary>A checkbox group inside a field: registration, the field's flags, validation on the array, labels and ids.</summary>
public sealed class CheckboxGroupFieldTests : IDisposable
{
    private readonly CheckboxTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "[data-dirty]")]
    [Trait("Requirement", "CKG-STA-12")]
    [Trait("Requirement", "CKG-DOM-03")]
    [Trait("Requirement", "CKG-FRM-18")]
    [Trait("Requirement", "CHK-FRM-20")]
    public async Task Data_dirty()
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "fruits")
            .Add(x => x.DefaultValue, ["apple"])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("apple") { Value = "apple" }, new GroupBox("banana") { Value = "banana" }]));
        IElement Group() => cut.Find("[role=group]");
        Assert.False(Group().HasAttribute("data-dirty"));

        await ToggleAsync(cut, "banana");
        Assert.Equal(string.Empty, Group().GetAttribute("data-dirty"));

        await ToggleAsync(cut, "banana");
        Assert.False(Group().HasAttribute("data-dirty"));
    }

    [Fact(DisplayName = "[data-filled] follows the group value even without a matching rendered checkbox")]
    [Trait("Requirement", "CKG-STA-11")]
    [Trait("Requirement", "CKG-STA-13")]
    [Trait("Requirement", "CKG-DOM-03")]
    [Trait("Requirement", "CHK-FRM-20")]
    public async Task Data_filled_follows_the_group_value_even_without_a_matching_rendered_checkbox()
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "fruits")
            .Add(x => x.DefaultValue, ["cherry"])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("apple") { Value = "apple" }]));
        Assert.Equal(string.Empty, cut.Find("[role=group]").GetAttribute("data-filled"));

        await ToggleAsync(cut, "apple");
        await ToggleAsync(cut, "apple");

        Assert.Equal(string.Empty, cut.Find("[role=group]").GetAttribute("data-filled"));
    }

    [Fact(DisplayName = "adds [data-filled] attribute when any checkbox is filled when inside a group")]
    [Trait("Requirement", "CHK-STA-10")]
    [Trait("Requirement", "CHK-DOM-05")]
    [Trait("Requirement", "CHK-API-06")]
    public async Task Adds_data_filled_attribute_when_any_checkbox_is_filled_when_inside_a_group()
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.DefaultValue, ["1", "2"])
            .Add(x => x.Boxes, [new GroupBox("button-1") { Name = "1" }, new GroupBox("button-2") { Name = "2" }]));
        IElement Box(string id) => cut.Find($"[data-testid={id}]");
        Assert.True(Box("button-1").HasAttribute("data-filled"));
        Assert.True(Box("button-2").HasAttribute("data-filled"));

        await ToggleAsync(cut, "button-1");
        Assert.True(Box("button-1").HasAttribute("data-filled"));
        Assert.True(Box("button-2").HasAttribute("data-filled"));

        await ToggleAsync(cut, "button-2");
        Assert.False(Box("button-1").HasAttribute("data-filled"));
        Assert.False(Box("button-2").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "does not leave a stale custom error when toggling checkboxes in a group")]
    [Trait("Requirement", "CKG-FRM-07")]
    [Trait("Requirement", "CKG-ARIA-05")]
    [Trait("Requirement", "CHK-ARIA-11")]
    public async Task Does_not_leave_a_stale_custom_error_when_toggling_checkboxes_in_a_group()
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "protocols")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args => ValueTask.FromResult<FieldValidationResult?>(((IReadOnlyList<string>)args.Value!).Count < 2 ? "pick two" : null))
            .Add(x => x.DefaultValue, [])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("cb-http") { Value = "http" }, new GroupBox("cb-https") { Value = "https" }]));

        await ToggleAsync(cut, "cb-http");
        Assert.Equal("true", Invalid(cut, "cb-http"));

        await ToggleAsync(cut, "cb-https");
        Assert.Null(Invalid(cut, "cb-http"));
        Assert.Null(Invalid(cut, "cb-https"));

        await ToggleAsync(cut, "cb-http");
        Assert.Equal("true", Invalid(cut, "cb-https"));
    }

    [Fact(DisplayName = "prop: validationMode=onChange")]
    [Trait("Requirement", "CKG-FRM-07")]
    [Trait("Requirement", "CKG-ARIA-05")]
    public async Task Prop_validationMode_onChange()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(((IReadOnlyList<string>)args.Value!).Contains("one") ? "error" : null);
            })
            .Add(x => x.DefaultValue, ["one"])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("one") { Value = "one" }, new GroupBox("two") { Value = "two" }, new GroupBox("three") { Value = "three" }]));
        string[] boxes = ["one", "two", "three"];
        Assert.All(boxes, id => Assert.Null(Invalid(cut, id)));

        await ToggleAsync(cut, "one");
        Assert.All(boxes, id => Assert.Null(Invalid(cut, id)));
        Assert.Equal([string.Empty], Joined(values));

        await ToggleAsync(cut, "two");
        Assert.All(boxes, id => Assert.Null(Invalid(cut, id)));
        Assert.Equal([string.Empty, "two"], Joined(values));

        await ToggleAsync(cut, "one");
        Assert.All(boxes, id => Assert.Equal("true", Invalid(cut, id)));
        Assert.Equal([string.Empty, "two", "two,one"], Joined(values));

        await ToggleAsync(cut, "three");
        Assert.All(boxes, id => Assert.Equal("true", Invalid(cut, id)));
    }

    [Fact(DisplayName = "validates with the group value when toggling the parent checkbox")]
    [Trait("Requirement", "CKG-FRM-07")]
    [Trait("Requirement", "CKG-STA-07")]
    public async Task Validates_with_the_group_value_when_toggling_the_parent_checkbox()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "fruits")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })
            .Add(x => x.AllValues, ["apple", "orange"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("apple") { Value = "apple" }, new GroupBox("orange") { Value = "orange" }]));

        await ToggleAsync(cut, "parent");
        Assert.Equal(["apple,orange"], Joined(values));

        await ToggleAsync(cut, "parent");
        Assert.Equal(["apple,orange", string.Empty], Joined(values));
    }

    [Fact(DisplayName = "revalidates when the controlled value changes externally")]
    [Trait("Requirement", "CKG-FRM-08")]
    [Trait("Requirement", "CKG-EVT-05")]
    public async Task Revalidates_when_the_controlled_value_changes_externally()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(((IReadOnlyList<string>)args.Value!).Contains("one") ? "error" : null);
            })
            .Add(x => x.Controlled, true)
            .Add(x => x.Value, [])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("one") { Value = "one" }, new GroupBox("two") { Value = "two" }]));
        Assert.All(new[] { "one", "two" }, id => Assert.Null(Invalid(cut, id)));
        int initial = values.Count;

        cut.Render(p => p.Add(x => x.Value, ["one"]));
        await test.SettleAllFormsAsync(cut);

        Assert.Equal(initial + 1, values.Count);
        Assert.Equal("one", Joined(values)[^1]);
        Assert.All(new[] { "one", "two" }, id => Assert.Equal("true", Invalid(cut, id)));
    }

    [Fact(DisplayName = "prop: validationMode=onBlur")]
    [Trait("Requirement", "CKG-FRM-09")]
    [Trait("Requirement", "CHK-FRM-20")]
    public async Task Prop_validationMode_onBlur()
    {
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.ValidationMode, ValidationMode.OnBlur)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(((IReadOnlyList<string>)args.Value!).Contains("one") ? "error" : null);
            })
            .Add(x => x.DefaultValue, ["one"])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("one") { Value = "one" }, new GroupBox("two") { Value = "two" }, new GroupBox("three") { Value = "three" }]));

        await ToggleAsync(cut, "one");
        Assert.Empty(values);
        await BlurAsync(cut, "one");
        Assert.Equal([string.Empty], Joined(values));
        Assert.Null(Invalid(cut, "one"));

        await ToggleAsync(cut, "three");
        Assert.Single(values);
        await BlurAsync(cut, "three");
        Assert.Equal([string.Empty, "three"], Joined(values));

        await ToggleAsync(cut, "one");
        await BlurAsync(cut, "one");
        Assert.Equal([string.Empty, "three", "three,one"], Joined(values));
        Assert.All(new[] { "one", "two", "three" }, id => Assert.Equal("true", Invalid(cut, id)));
    }

    [Theory(DisplayName = "keeps checkbox ids unique when the group shares one Field.Root (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CKG-DOM-06")]
    [Trait("Requirement", "CKG-ARIA-06")]
    [Trait("Requirement", "CHK-DOM-15")]
    public void Keeps_checkbox_ids_unique_when_the_group_shares_one_Field_Root(bool nativeButton)
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apples")
            .Add(x => x.ShowLabel, true)
            .Add(x => x.AllValues, ["fuji", "gala"])
            .Add(x => x.Boxes,
            [
                new GroupBox("parent") { Parent = true, NativeButton = nativeButton },
                new GroupBox("fuji") { Value = "fuji", NativeButton = nativeButton },
                new GroupBox("gala") { Value = "gala", NativeButton = nativeButton },
            ]));

        ExpectUniqueIds(cut, nativeButton ? 4 : 7);
        Assert.Equal(
            [cut.Find("[data-testid=fuji]").GetAttribute("id")!, cut.Find("[data-testid=gala]").GetAttribute("id")!],
            cut.Find("[data-testid=parent]").GetAttribute("aria-controls")!.Split(' '));
    }

    [Theory(DisplayName = "keeps ids unique without allValues in a shared Field.Root (nativeButton=%s)")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CKG-DOM-06")]
    [Trait("Requirement", "CKG-EDGE-09")]
    public void Keeps_ids_unique_without_allValues_in_a_shared_Field_Root(bool nativeButton)
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apples")
            .Add(x => x.ShowLabel, true)
            .Add(x => x.Boxes, [new GroupBox("fuji") { Value = "fuji", NativeButton = nativeButton }, new GroupBox("gala") { Value = "gala", NativeButton = nativeButton }]));

        ExpectUniqueIds(cut, nativeButton ? 3 : 5);
    }

    [Fact(DisplayName = "labels the group rather than pointing Field.Label at one checkbox inside it")]
    [Trait("Requirement", "CKG-ARIA-02")]
    [Trait("Requirement", "CKG-ARIA-03")]
    public void Labels_the_group_rather_than_pointing_Field_Label_at_one_checkbox_inside_it()
    {
        // The state after the claim (the prerendered half is tests/browser/checkbox-group.spec.ts).
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apples")
            .Add(x => x.ShowLabel, true)
            .Add(x => x.AllValues, ["fuji", "gala"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("fuji") { Value = "fuji" }, new GroupBox("gala") { Value = "gala" }]));

        IElement label = cut.Find("[data-testid=field-label]");
        Assert.False(label.HasAttribute("for"));
        Assert.Equal(label.GetAttribute("id"), cut.Find("[role=group]").GetAttribute("aria-labelledby"));

        // CKG-ARIA-07: the group's label names no box.
        Assert.All(new[] { "parent", "fuji", "gala" }, id => Assert.NotEqual(label.GetAttribute("id"), cut.Find($"[data-testid={id}]").GetAttribute("aria-labelledby")));
    }

    [Fact(DisplayName = "implicit association")]
    [Trait("Requirement", "CKG-ARIA-07")]
    [Trait("Requirement", "CKG-FRM-18")]
    [Trait("Requirement", "CKG-STR-04")]
    [Trait("Requirement", "CHK-FRM-21")]
    [Trait("Requirement", "CHK-ARIA-06")]
    [Trait("Requirement", "CHK-EVT-06")]
    public async Task Implicit_association()
    {
        GroupBox granny = new("granny-smith-apple") { Value = "granny-smith-apple" };
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.DefaultValue, ["fuji-apple", "gala-apple"])
            .Add(x => x.Items, ItemLayout.WrappingLabel)
            .Add(x => x.Boxes, [new GroupBox("fuji-apple") { Value = "fuji-apple" }, new GroupBox("gala-apple") { Value = "gala-apple" }, granny]));

        ExpectItemAssociation(cut, ["fuji-apple", "gala-apple", "granny-smith-apple"], describedBy: false);

        // The label click is native (it clicks the input it points at); one toggle, one callback.
        await ToggleAsync(cut, "granny-smith-apple");
        Assert.Single(granny.Changes);
    }

    [Fact(DisplayName = "explicit association")]
    [Trait("Requirement", "CKG-ARIA-07")]
    [Trait("Requirement", "CKG-FRM-18")]
    [Trait("Requirement", "CHK-FRM-21")]
    [Trait("Requirement", "CHK-ARIA-10")]
    public async Task Explicit_association()
    {
        GroupBox gala = new("gala-apple") { Value = "gala-apple" };
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.DefaultValue, ["fuji-apple", "gala-apple"])
            .Add(x => x.Items, ItemLayout.SiblingLabel)
            .Add(x => x.Boxes, [new GroupBox("fuji-apple") { Value = "fuji-apple" }, gala]));

        ExpectItemAssociation(cut, ["fuji-apple", "gala-apple"], describedBy: true);

        await ToggleAsync(cut, "gala-apple");
        Assert.Single(gala.Changes);
    }

    [Fact(DisplayName = "links the group and individual checkboxes")]
    [Trait("Requirement", "CKG-ARIA-04")]
    [Trait("Requirement", "CKG-API-08")]
    [Trait("Requirement", "CHK-ARIA-10")]
    public void Links_the_group_and_individual_checkboxes()
    {
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "apple")
            .Add(x => x.DefaultValue, [])
            .Add(x => x.ShowGroupDescription, true)
            .Add(x => x.GroupAttributes, new Dictionary<string, object> { ["aria-describedby"] = "external-description" })
            .Add(x => x.Items, ItemLayout.WrappingLabel)
            .Add(x => x.Boxes, [new GroupBox("fuji-apple") { Value = "fuji-apple", DescribedBy = "checkbox-description" }]));

        string groupDescription = cut.Find("[data-testid=group-description]").GetAttribute("id")!;
        Assert.Equal($"external-description {groupDescription}", cut.Find("[role=group]").GetAttribute("aria-describedby"));
        Assert.Equal($"checkbox-description {groupDescription}", cut.Find("[data-testid=fuji-apple]").GetAttribute("aria-describedby"));
    }

    [Fact(DisplayName = "validates an initially empty group through the imperative action")]
    [Trait("Requirement", "CKG-FRM-12")]
    [Trait("Requirement", "CKG-FRM-02")]
    public async Task Validates_an_initially_empty_group_through_the_imperative_action()
    {
        List<FieldValidationArguments> calls = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "group")
            .Add(x => x.DefaultValue, [])
            .Add(x => x.Validate, args =>
            {
                calls.Add(args);
                return ValueTask.FromResult<FieldValidationResult?>("invalid");
            }));
        calls.Clear();

        await ValidateAsync(cut);

        FieldValidationArguments call = Assert.Single(calls);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(call.Value));
    }

    [Fact(DisplayName = "port: the group registers only while its field has a name and it is enabled")]
    [Trait("Requirement", "CKG-FRM-01")]
    [Trait("Requirement", "CHK-FRM-06")]
    public async Task Port_the_group_registers_only_while_its_field_has_a_name()
    {
        // A nameless field: no registration, so an on-change toggle runs no validator (checkbox-group §16 Q1).
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })
            .Add(x => x.Boxes, [new GroupBox("a") { Value = "a" }]));

        await ValidateAsync(cut);

        // Without a registration the imperative validation commits the field's last value, not the group's array.
        Assert.DoesNotContain(values, v => v is IReadOnlyList<string>);
    }

    [Fact(DisplayName = "port: the members' hidden inputs are the field's validation inputs, and a parent's is not")]
    [Trait("Requirement", "CHK-FRM-19")]
    [Trait("Requirement", "CHK-API-05")]
    public async Task Port_the_members_hidden_inputs_are_the_fields_validation_inputs()
    {
        // A required member whose input reports valueMissing makes the field invalid on an imperative validation: the
        // representative input is a member's (FFI-ALG-04), refreshed through the group's registration.
        test.InputValidity = NativeValidity.Default with { Valid = false, ValueMissing = true, ValidationMessage = "Tick it" };
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.FieldName, "fruits")
            .Add(x => x.AllValues, ["apple"])
            .Add(x => x.Boxes, [new GroupBox("parent") { Parent = true }, new GroupBox("apple") { Value = "apple", Required = true }]));

        await ValidateAsync(cut);

        Assert.Equal("true", Invalid(cut, "apple"));
        Assert.Equal("true", Invalid(cut, "parent"));

        // Each member's hidden input hands focus to its own root.
        string appleRoot = cut.Find("[data-testid=apple]").GetAttribute("id")!;
        Assert.Contains(test.FormOptions, o => !o.ReportEvents && o.FocusTarget == appleRoot);
    }

    [Fact(DisplayName = "port: CheckboxGroup.ValueExpression notifies the cascaded EditContext and names a nameless field")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "CKG-FRM-01")]
    public async Task Port_ValueExpression_notifies_the_cascaded_EditContext()
    {
        Order model = new();
        EditContext editContext = new(model);
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        Expression<Func<IReadOnlyList<string>>> expression = () => model.Toppings;
        List<object?> values = [];
        IRenderedComponent<CheckboxGroupFieldHost> cut = Context.Render<CheckboxGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, expression)
            .Add(x => x.ValidationMode, ValidationMode.OnChange)
            .Add(x => x.Validate, args =>
            {
                values.Add(args.Value);
                return ValueTask.FromResult<FieldValidationResult?>(null);
            })
            .Add(x => x.Boxes, [new GroupBox("cheese") { Value = "cheese" }]));

        await ToggleAsync(cut, "cheese");

        Assert.Equal([new FieldIdentifier(model, nameof(Order.Toppings))], changed);

        // ADR 05 Amendments 2026-10-22 item (2): the identifier's name names the nameless field's registration.
        Assert.Equal(["cheese"], Joined(values));
    }

    [Fact(DisplayName = "port: a member's CheckedExpression is inert, because the group's ValueExpression is the field")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_a_members_CheckedExpression_is_inert()
    {
        Order model = new();
        EditContext editContext = new(model);
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        Expression<Func<bool>> expression = () => model.Accepted;
        IRenderedComponent<CheckboxGroup> cut = Context.Render<CheckboxGroup>(p => p
            .AddCascadingValue(editContext)
            .AddChildContent<CheckboxRoot>(b => b.Add(x => x.Value, "x").Add(x => x.CheckedExpression, expression).AddUnmatched("data-testid", "x")));

        await ToggleAsync(cut, "x");

        Assert.Equal(0, changes);
        Assert.Equal("true", cut.Find("[data-testid=x]").GetAttribute("aria-checked"));
    }

    private static string? Invalid(IRenderedComponent<CheckboxGroupFieldHost> cut, string testId) =>
        cut.Find($"[data-testid={testId}]").GetAttribute("aria-invalid");

    private static string[] Joined(IEnumerable<object?> values) =>
        [.. values.Select(v => string.Join(',', (IReadOnlyList<string>)v!))];

    private static void ExpectUniqueIds(IRenderedComponent<CheckboxGroupFieldHost> cut, int expectedCount)
    {
        string[] ids = [.. cut.FindAll("[id]").Select(e => e.GetAttribute("id")!)];
        Assert.Equal(expectedCount, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    private static void ExpectItemAssociation(IRenderedComponent<CheckboxGroupFieldHost> cut, string[] boxes, bool describedBy)
    {
        IReadOnlyList<IElement> labels = cut.FindAll("[data-testid=label]");
        IReadOnlyList<IElement> descriptions = cut.FindAll("[data-testid=description]");
        for (int i = 0; i < boxes.Length; i++)
        {
            IElement box = cut.Find($"[data-testid={boxes[i]}]");
            IElement input = box.NextElementSibling!;
            Assert.NotNull(labels[i].GetAttribute("for"));
            Assert.Equal(input.GetAttribute("id"), labels[i].GetAttribute("for"));
            Assert.NotNull(labels[i].GetAttribute("id"));
            Assert.Equal(labels[i].GetAttribute("id"), box.GetAttribute("aria-labelledby"));
            if (describedBy)
            {
                Assert.Equal(descriptions[i].GetAttribute("id"), box.GetAttribute("aria-describedby"));
            }
        }
    }

    private async Task ToggleAsync<TComponent>(IRenderedComponent<TComponent> cut, string testId)
        where TComponent : IComponent
    {
        await test.ToggleAsync(cut, testId);
        await test.SettleAllFormsAsync(cut);
    }

    private async Task BlurAsync(IRenderedComponent<CheckboxGroupFieldHost> cut, string testId)
    {
        CheckboxRoot box = GroupTesting.Box(cut, testId);
        await test.BlurAsync(cut, box);
    }

    // The imperative validation waits for the group's refresh of its members' inputs, which the module answers.
    private async Task ValidateAsync(IRenderedComponent<CheckboxGroupFieldHost> cut)
    {
        Task pending = Task.CompletedTask;
        await cut.InvokeAsync(() => pending = cut.Instance.Field!.ValidateAsync());
        await test.SettleAllFormsAsync(cut);
        await pending;
    }

    private sealed class Order
    {
        public IReadOnlyList<string> Toppings { get; set; } = [];

        public bool Accepted { get; set; }
    }
}
