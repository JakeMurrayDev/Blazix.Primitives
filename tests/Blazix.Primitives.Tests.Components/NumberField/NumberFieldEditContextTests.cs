using System.Globalization;
using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Verification item 5 of ADR 05 (Amendments 2026-10-22, issue 76) for NumberField at L2 (Amendments 2026-11-18 item 2,
// decision 14 (a), chosen by the user 2026-11-18), the RadioGroupEditContextTests pattern: ValueExpression with a cascaded
// EditContext notifies the context of every committed change, names a nameless field, maps the context's messages for its
// identifier into the field's errors, and is inert without a context. A change is a stepper click (the button twin's report).
/// <summary>A number field bound with <c>@bind-Value</c> inside an <see cref="EditContext"/>.</summary>
public sealed class NumberFieldEditContextTests : IDisposable
{
    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();
    private readonly Order model = new();
    private readonly EditContext editContext;

    public NumberFieldEditContextTests()
    {
        editContext = new EditContext(model);
    }

    private FieldIdentifier Quantity => new(model, nameof(Order.Quantity));

    private Expression<Func<double?>> QuantityExpression => () => model.Quantity;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: ValueExpression notifies the cascaded EditContext of every committed change")]
    [Trait("Verifies", "ADR05-5")]
    public async Task ValueExpression_notifies_the_cascaded_EditContext_of_every_committed_change()
    {
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        IRenderedComponent<NumberFieldRoot> cut = RenderRoot(p => p.Add(x => x.DefaultValue, 1));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);
        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal([Quantity, Quantity], changed);
    }

    [Fact(DisplayName = "port: a canceled step does not notify the EditContext")]
    [Trait("Verifies", "ADR05-5")]
    public async Task A_canceled_step_does_not_notify_the_EditContext()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        IRenderedComponent<NumberFieldRoot> cut = RenderRoot(p => p
            .Add(x => x.DefaultValue, 1)
            .Add(x => x.OnValueChange, (ValueChangeEventArgs<double?> e) => e.Details.Cancel()));

        await test.ClickAsync(cut, cut.FindComponent<NumberFieldIncrement>().Instance);

        Assert.Equal("1", test.Text);
        Assert.Equal(0, changes);
    }

    [Fact(DisplayName = "port: the effective name defaults to the identifier's field name")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "NUM-API-16")]
    public void The_effective_name_defaults_to_the_identifiers_field_name()
    {
        IRenderedComponent<NumberFieldRoot> unnamed = RenderRoot();
        Assert.Equal("Quantity", unnamed.Find("input[type=number]").GetAttribute("name"));

        IRenderedComponent<NumberFieldRoot> named = RenderRoot(p => p.Add(x => x.Name, "own"));
        Assert.Equal("own", named.Find("input[type=number]").GetAttribute("name"));
    }

    [Fact(DisplayName = "port: the context's messages for the identifier show in Field.Error and data-invalid")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "FFI-FRM-25")]
    public async Task The_contexts_messages_for_the_identifier_show_in_Field_Error()
    {
        ValidationMessageStore store = new(editContext);
        IRenderedComponent<FieldRoot> cut = test.Context.Render<FieldRoot>(p => p
            .AddCascadingValue(editContext)
            .Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldRoot>(0);
                b.AddComponentParameter(1, nameof(NumberFieldRoot.Culture), s_enUs);
                b.AddComponentParameter(2, nameof(NumberFieldRoot.ValueExpression), QuantityExpression);
                b.AddComponentParameter(3, nameof(NumberFieldRoot.ChildContent), (RenderFragment)(parts =>
                {
                    parts.OpenComponent<NumberFieldInput>(0);
                    parts.CloseComponent();
                }));
                b.CloseComponent();
                b.OpenComponent<FieldError>(4);
                b.AddComponentParameter(5, "data-testid", "error");
                b.CloseComponent();
            })));
        Assert.False(cut.Find("input[type=text]").HasAttribute("data-invalid"));

        await cut.InvokeAsync(() =>
        {
            store.Add(Quantity, "Too many");
            editContext.NotifyValidationStateChanged();
        });

        Assert.True(cut.Find("input[type=text]").HasAttribute("data-invalid"));
        Assert.Equal("true", cut.Find("input[type=text]").GetAttribute("aria-invalid"));
        Assert.Equal("Too many", cut.Find("[data-testid=error]").TextContent);
    }

    private IRenderedComponent<NumberFieldRoot> RenderRoot(Action<ComponentParameterCollectionBuilder<NumberFieldRoot>>? parameters = null)
    {
        return test.Context.Render<NumberFieldRoot>(p =>
        {
            p.AddCascadingValue(editContext);
            p.Add(x => x.Culture, s_enUs);
            p.Add(x => x.ValueExpression, QuantityExpression);
            p.Add(x => x.ChildContent, (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.CloseComponent();
                b.OpenComponent<NumberFieldIncrement>(1);
                b.CloseComponent();
            }));
            parameters?.Invoke(p);
        });
    }

    private sealed class Order
    {
        public double? Quantity { get; set; }
    }
}
