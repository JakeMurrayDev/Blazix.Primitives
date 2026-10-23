using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using static Blazix.Primitives.Tests.Components.RadioGroupFieldHost;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Verification item 5 of ADR 05 (Amendments 2026-10-22, issue 76) for RadioGroup at L2, through track 1's
// EditContextParticipation: ValueExpression with a cascaded EditContext notifies the context of every committed change,
// names a nameless group, maps the context's messages for its identifier into the field's external errors (Field.Error,
// data-invalid), and is inert without either. The L4 title is radio-group.spec.ts's on the radio-group-edit-form fixture.
/// <summary>A radio group bound with <c>@bind-Value</c> inside an <see cref="EditContext"/>.</summary>
public sealed class RadioGroupEditContextTests : IDisposable
{
    private readonly RadioTestContext test = new();
    private readonly Order model = new();
    private readonly EditContext editContext;

    public RadioGroupEditContextTests()
    {
        editContext = new EditContext(model);
    }

    private BunitContext Context => test.Context;

    private FieldIdentifier Fruit => new(model, nameof(Order.Fruit));

    private Expression<Func<string?>> FruitExpression => () => model.Fruit;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: ValueExpression notifies the cascaded EditContext of every committed change")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_ValueExpression_notifies_the_cascaded_EditContext_of_every_committed_change()
    {
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.Radios, [new HostRadio("apple"), new HostRadio("pear")]));

        await test.ToggleAsync(cut, 0);
        await test.SettleAllFormsAsync(cut);
        await test.ToggleAsync(cut, 1);
        await test.SettleAllFormsAsync(cut);

        Assert.Equal([Fruit, Fruit], changed);
    }

    [Fact(DisplayName = "port: a canceled selection does not notify the EditContext")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_a_canceled_selection_does_not_notify_the_EditContext()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        IRenderedComponent<RadioGroup<string>> cut = Context.Render<RadioGroup<string>>(p => p
            .AddCascadingValue(editContext)
            .Add(x => x.ValueExpression, () => model.Fruit!)
            .Add(x => x.OnValueChange, args => args.Details.Cancel())
            .AddChildContent(RadioTestContext.StringRadios("apple")));

        await test.ToggleAsync(cut, 0);

        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "port: the effective name defaults to the identifier's field name, inside and outside a field")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "RDG-API-07")]
    public void Port_the_effective_name_defaults_to_the_identifiers_field_name()
    {
        IRenderedComponent<RadioGroupFieldHost> inField = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.Radios, [new HostRadio("apple")]));
        Assert.Equal("Fruit", inField.Find("input[type=radio]").GetAttribute("name"));

        IRenderedComponent<RadioGroup<string>> outside = Context.Render<RadioGroup<string>>(p => p
            .AddCascadingValue(editContext)
            .Add(x => x.ValueExpression, () => model.Fruit!)
            .AddChildContent(RadioTestContext.StringRadios("apple")));
        Assert.Equal("Fruit", outside.Find("input[type=radio]").GetAttribute("name"));

        // The group's own name, then the field's, win over the identifier's.
        IRenderedComponent<RadioGroupFieldHost> named = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.GroupName, "own")
            .Add(x => x.Radios, [new HostRadio("apple")]));
        Assert.Equal("own", named.Find("input[type=radio]").GetAttribute("name"));

        IRenderedComponent<RadioGroupFieldHost> fieldNamed = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.FieldName, "field")
            .Add(x => x.GroupName, "own")
            .Add(x => x.Radios, [new HostRadio("apple")]));
        Assert.Equal("field", fieldNamed.Find("input[type=radio]").GetAttribute("name"));
    }

    [Fact(DisplayName = "port: the identifier's name names a nameless field's registration")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "RDG-FRM-05")]
    public async Task Port_the_identifiers_name_names_a_nameless_fields_registration()
    {
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.InForm, true)
            .Add(x => x.CaptureFormSubmit, true)
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.DefaultValue, "apple")
            .Add(x => x.Radios, [new HostRadio("apple"), new HostRadio("pear")]));

        await test.SubmitAsync(cut);

        FormValues values = Assert.Single(cut.Instance.FormSubmits);
        Assert.Equal("apple", values["Fruit"]);
    }

    [Fact(DisplayName = "port: the context's messages for the identifier show in Field.Error and data-invalid")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "FFI-FRM-25")]
    public async Task Port_the_contexts_messages_for_the_identifier_show_in_Field_Error()
    {
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<RadioGroupFieldHost> cut = Context.Render<RadioGroupFieldHost>(p => p
            .Add(x => x.EditContext, editContext)
            .Add(x => x.ValueExpression, FruitExpression)
            .Add(x => x.ShowError, true)
            .Add(x => x.Radios, [new HostRadio("apple")]));
        Assert.Empty(cut.FindAll("[data-testid=error]"));

        await cut.InvokeAsync(() =>
        {
            store.Add(Fruit, "Pick a fruit.");
            editContext.NotifyValidationStateChanged();
        });

        Assert.Contains("Pick a fruit.", cut.Find("[data-testid=error]").TextContent, StringComparison.Ordinal);
        Assert.Equal(string.Empty, cut.Find("[role=radiogroup]").GetAttribute("data-invalid"));
        Assert.Equal("true", cut.Find("[role=radiogroup]").GetAttribute("aria-invalid"));

        await cut.InvokeAsync(() =>
        {
            store.Clear();
            editContext.NotifyValidationStateChanged();
        });
        await test.CompleteExitsAsync(cut);

        Assert.Empty(cut.FindAll("[data-testid=error]"));
        Assert.False(cut.Find("[role=radiogroup]").HasAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "port: without an EditContext or an expression the parameters are inert")]
    [Trait("Verifies", "ADR05-5")]
    public async Task Port_without_an_EditContext_or_an_expression_the_parameters_are_inert()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;

        IRenderedComponent<RadioGroup<string>> noContext = Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.ValueExpression, () => model.Fruit!)
            .AddChildContent(RadioTestContext.StringRadios("apple")));
        await test.ToggleAsync(noContext, 0);
        Assert.False(noContext.Find("input[type=radio]").HasAttribute("name"));

        IRenderedComponent<RadioGroup<string>> noExpression = Context.Render<RadioGroup<string>>(p => p
            .AddCascadingValue(editContext)
            .AddChildContent(RadioTestContext.StringRadios("apple")));
        await test.ToggleAsync(noExpression, 1);
        Assert.False(noExpression.Find("input[type=radio]").HasAttribute("name"));

        Assert.Equal(0, changes);
    }

    /// <summary>The model the tests bind to.</summary>
    public sealed class Order
    {
        public string? Fruit { get; set; }
    }
}
