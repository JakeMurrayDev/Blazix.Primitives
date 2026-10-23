using Blazix.Primitives.Interop;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// CheckboxRoot.test.tsx's Form titles (CHK-FRM-08, CHK-FRM-12, CHK-FRM-13, CHK-FOC-06, CHK-API-10), now that Form is on
// main. A submission is the module's report of the state it read from every enrolled hidden input; Field.Error is not on
// main (PR 1c), so the field's verdict is read from the root's aria-invalid.
/// <summary>A box in a field inside a form: submission, revalidation after it, and the form's errors.</summary>
public sealed class CheckboxFormTests : IDisposable
{
    private static readonly NativeValidity s_missing = NativeValidity.Default with { Valid = false, ValueMissing = true, ValidationMessage = "Tick this box." };

    private readonly CheckboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "prop: validationMode=onSubmit")]
    [Trait("Requirement", "CHK-FRM-12")]
    [Trait("Requirement", "CHK-ARIA-11")]
    [Trait("Requirement", "CHK-DOM-02")]
    public async Task Prop_validationMode_onSubmit()
    {
        IRenderedComponent<CheckboxFormHost> cut = test.Context.Render<CheckboxFormHost>(p => p.Add(x => x.Required, true));
        string? Invalid() => cut.Find("[data-testid=checkbox]").GetAttribute("aria-invalid");
        Assert.Null(Invalid());

        await ToggleAsync(cut, true);
        Assert.True(cut.Find("[data-testid=checkbox]").HasAttribute("data-checked"));
        await ToggleAsync(cut, false);
        Assert.True(cut.Find("[data-testid=checkbox]").HasAttribute("data-unchecked"));
        Assert.Null(Invalid());

        await test.SubmitAsync(cut, _ => s_missing);
        Assert.Equal("true", Invalid());

        await ToggleAsync(cut, true);
        Assert.Null(Invalid());

        await ToggleAsync(cut, false);
        Assert.Equal("true", Invalid());

        await ToggleAsync(cut, true);
        Assert.Null(Invalid());
    }

    [Fact(DisplayName = "port: a required box blocks the submit of its form, which focuses the root")]
    [Trait("Requirement", "CHK-FRM-08")]
    [Trait("Requirement", "CHK-FOC-06")]
    [Trait("Requirement", "CHK-API-10")]
    public async Task Port_a_required_box_blocks_the_submit_of_its_form()
    {
        // "triggers native HTML validation on submit" without its Field.Error clause, which waits for PR 1c.
        IRenderedComponent<CheckboxFormHost> cut = test.Context.Render<CheckboxFormHost>(p => p.Add(x => x.FieldName, "test").Add(x => x.Required, true));

        FormSubmitAnswer answer = await test.SubmitAsync(cut, _ => s_missing);

        Assert.Equal(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Equal([cut.Find("[data-testid=checkbox]").GetAttribute("id")!], answer.Focus);
        Assert.Equal("true", cut.Find("[data-testid=checkbox]").GetAttribute("aria-invalid"));
        Assert.Empty(cut.Instance.Submits);
    }

    [Fact(DisplayName = "port: a submit with the required box ticked is not blocked")]
    [Trait("Requirement", "CHK-FRM-08")]
    public async Task Port_a_submit_with_the_required_box_ticked_is_not_blocked()
    {
        IRenderedComponent<CheckboxFormHost> cut = test.Context.Render<CheckboxFormHost>(p => p.Add(x => x.FieldName, "test").Add(x => x.Required, true));
        await ToggleAsync(cut, true);

        FormSubmitAnswer answer = await test.SubmitAsync(cut, _ => NativeValidity.Default);

        Assert.NotEqual(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Single(cut.Instance.Submits);
        Assert.Null(cut.Find("[data-testid=checkbox]").GetAttribute("aria-invalid"));
    }

    [Fact(DisplayName = "clears external errors on change")]
    [Trait("Requirement", "CHK-FRM-13")]
    [Trait("Requirement", "CHK-ARIA-11")]
    public async Task Clears_external_errors_on_change()
    {
        // The Field.Error clause waits for PR 1c; the aria-invalid clause is the field's state.
        IRenderedComponent<CheckboxFormHost> cut = test.Context.Render<CheckboxFormHost>(p => p
            .Add(x => x.FieldName, "test")
            .Add(x => x.Errors, new FormErrors { ["test"] = "test" }));
        Assert.Equal("true", cut.Find("[data-testid=checkbox]").GetAttribute("aria-invalid"));

        await ToggleAsync(cut, true);

        Assert.Null(cut.Find("[data-testid=checkbox]").GetAttribute("aria-invalid"));
    }

    // A toggle, then the hidden input's refresh answered with the validity its new value has.
    private async Task ToggleAsync(IRenderedComponent<CheckboxFormHost> cut, bool @checked)
    {
        test.InputValidity = @checked ? NativeValidity.Default : s_missing;
        await test.ToggleAsync(cut, @checked);
        await test.SettleFormAsync(cut);
    }
}
