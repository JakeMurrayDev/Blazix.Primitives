using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// FFI-FRM-04, FFI-FRM-24, CKG-FRM-05, CKG-FOC-02 at L2: the state the form module reads from each member's hidden input
// in the submit task reaches that input's validation state (the input sink, settled by the orchestrator 2026-10-23), so
// the group's field validates with its representative input. The browser half is checkbox-group.spec.ts, Form (track 2).
/// <summary>A checkbox group in a field inside a form, at submission.</summary>
public sealed class CheckboxGroupFormTests : IDisposable
{
    private static readonly NativeValidity s_missing = NativeValidity.Default with { Valid = false, ValueMissing = true, ValidationMessage = "Tick this box." };

    private readonly CheckboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the representative member's state at submit reaches the group's field")]
    [Trait("Requirement", "CKG-FRM-05")]
    [Trait("Requirement", "CKG-FOC-02")]
    public async Task Port_the_representative_members_state_at_submit_reaches_the_groups_field()
    {
        List<FormSubmitEventArgs> submits = [];
        IRenderedComponent<Form> cut = test.Context.Render<Form>(p => p
            .Add(x => x.OnSubmit, args => submits.Add(args))
            .AddChildContent<FieldRoot>(field => field
                .Add(x => x.Name, "fruits")
                .AddChildContent<CheckboxGroup>(group => group
                    .Add(x => x.DefaultValue, ["banana"])
                    .AddChildContent((RenderFragment)(b =>
                    {
                        b.OpenComponent<CheckboxRoot>(0);
                        b.AddAttribute(1, "data-testid", "apple");
                        b.AddComponentParameter(2, nameof(CheckboxRoot.Value), "apple");
                        b.AddComponentParameter(3, nameof(CheckboxRoot.Required), true);
                        b.CloseComponent();
                        b.OpenComponent<CheckboxRoot>(4);
                        b.AddAttribute(5, "data-testid", "banana");
                        b.AddComponentParameter(6, nameof(CheckboxRoot.Value), "banana");
                        b.CloseComponent();
                    })))));
        IElement Apple() => cut.Find("[data-testid=apple]");
        string appleInput = Apple().NextElementSibling!.GetAttribute("id")!;

        // A pristine group: the members reported nothing yet, so the required member's valueMissing is the submit's read.
        FormSubmitAnswer answer = await test.SubmitAsync(cut, id => id == appleInput ? s_missing : NativeValidity.Default);

        Assert.Equal(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Equal([Apple().GetAttribute("id")!], answer.Focus);
        Assert.Equal("true", Apple().GetAttribute("aria-invalid"));
        Assert.Equal("true", cut.Find("[data-testid=banana]").GetAttribute("aria-invalid"));
        Assert.Empty(submits);

        answer = await test.SubmitAsync(cut, _ => NativeValidity.Default);

        Assert.NotEqual(FormSubmitAnswer.BlockedVerdict, answer.Verdict);
        Assert.Single(submits);
    }
}
