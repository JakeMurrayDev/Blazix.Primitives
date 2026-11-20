using System.Linq.Expressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Verification item 5 of ADR 05 (Amendments 2026-10-22, issue 76) for OtpField at L2 (Amendments 2026-11-18 item 2,
// OtpFieldField and ValueExpression; issue 278, M6 track 7), the RadioGroupEditContextTests pattern: ValueExpression with a
// cascaded EditContext notifies the context of every committed change, a canceled keyboard edit notifies nothing, and a
// nameless field takes the identifier's name. A change is an edit report of the module.
/// <summary>A one-time code field bound inside an <see cref="EditContext"/>.</summary>
public sealed class OtpFieldEditContextTests : IDisposable
{
    private readonly OtpFieldTestContext test = new();
    private readonly Login model = new();
    private readonly EditContext editContext;

    public OtpFieldEditContextTests()
    {
        editContext = new EditContext(model);
    }

    private FieldIdentifier Code => new(model, nameof(Login.Code));

    private Expression<Func<string>> CodeExpression => () => model.Code;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: ValueExpression notifies the cascaded EditContext of every committed change")]
    [Trait("Verifies", "ADR05-5")]
    public async Task ValueExpression_notifies_the_cascaded_EditContext_of_every_committed_change()
    {
        List<FieldIdentifier> changed = [];
        editContext.OnFieldChanged += (_, e) => changed.Add(e.FieldIdentifier);
        IRenderedComponent<CascadingValue<EditContext>> cut = Render();

        await test.EditAsync(cut, 2, "input", "3", 1);
        await test.EditAsync(cut, 3, "paste", "45", 2);

        Assert.Equal([Code, Code], changed);
        Assert.Equal("12345", test.OtpOptions[^1].Value);
    }

    [Fact(DisplayName = "port: a canceled keyboard edit does not notify the EditContext")]
    [Trait("Verifies", "ADR05-5")]
    public async Task A_canceled_keyboard_edit_does_not_notify_the_EditContext()
    {
        int changes = 0;
        editContext.OnFieldChanged += (_, _) => changes++;
        IRenderedComponent<CascadingValue<EditContext>> cut = Render(r => r.OnValueChange = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, e => e.Details.Cancel()));

        await test.EditAsync(cut, 1, "backspace", string.Empty, 1);

        Assert.Equal(("12", 0), (test.OtpOptions[^1].Value, changes));
    }

    [Fact(DisplayName = "port: the effective name defaults to the identifier's field name")]
    [Trait("Verifies", "ADR05-5")]
    [Trait("Requirement", "OTP-API-14")]
    public void The_effective_name_defaults_to_the_identifiers_field_name()
    {
        IRenderedComponent<CascadingValue<EditContext>> cut = Render();

        Assert.Equal("Code", cut.Find("input[aria-hidden]").GetAttribute("name"));
    }

    private IRenderedComponent<CascadingValue<EditContext>> Render(Action<RootParameters>? configure = null)
    {
        RootParameters parameters = new();
        configure?.Invoke(parameters);
        return test.Context.Render<CascadingValue<EditContext>>(p => p
            .Add(c => c.Value, editContext)
            .Add(c => c.IsFixed, true)
            .AddChildContent<OtpFieldRoot>(root => root
                .Add(r => r.Length, 6)
                .Add(r => r.DefaultValue, "12")
                .Add(r => r.ValueExpression, CodeExpression)
                .Add(r => r.OnValueChange, parameters.OnValueChange)
                .AddChildContent(OtpFieldTestContext.Slots(6))));
    }

    private sealed class RootParameters
    {
        public EventCallback<ValueChangeEventArgs<string>> OnValueChange { get; set; }
    }

    private sealed class Login
    {
        public string Code { get; set; } = string.Empty;
    }
}
