using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// Design point 11 of 6b, chosen by the user 2026-11-19 ("Re-sync on change only", issue 277): the input re-derives its
// display text only when Value, Format or Culture changed, or after one of the field's own requests (NUM-STA-06). Base UI's
// sync runs on every Root render (NumberFieldRoot.tsx:312-324), which under React's render model happens only when the
// field or its parent's state changed; a Blazor parent re-renders after every EventCallback with a receiver.
/// <summary>The display text across a parent's re-render.</summary>
public sealed class NumberFieldTextSyncTests : IDisposable
{
    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a cancelled blur keeps the typed text across the re-render of a parent whose callback has a receiver")]
    [Trait("Requirement", "NUM-STA-06")]
    [Trait("Requirement", "NUM-EVT-04")]
    public async Task A_cancelled_blur_keeps_the_typed_text_across_a_parents_rerender()
    {
        IRenderedComponent<CancellingHost> cut = test.Context.Render<CancellingHost>();

        await test.ChangeAsync(cut, "1.239");
        await test.BlurAsync(cut, "1.239", revision: 1);

        Assert.True(cut.Instance.Renders > 1);
        Assert.Empty(cut.Instance.Commits);
        Assert.Equal("1.239", test.Text);
    }

    [Fact(DisplayName = "port: a parent's new value still re-derives the text, and a parent's re-render with the same value does not")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task A_parents_new_value_rederives_the_text()
    {
        IRenderedComponent<CancellingHost> cut = test.Context.Render<CancellingHost>();
        await test.ChangeAsync(cut, "1.239");
        await test.BlurAsync(cut, "1.239", revision: 1);
        Assert.Equal("1.239", test.Text);

        cut.Instance.Value = 7;
        await cut.InvokeAsync(cut.Instance.Rerender);

        Assert.Equal("7", test.Text);
    }

    // NumberFieldInput.test.tsx:1219's root, its cancelling onValueChange an EventCallback with a receiver, as a Blazor page
    // writes it, so the host renders again after every change request.
    private sealed class CancellingHost : ComponentBase
    {
        private static readonly NumberFormatOptions s_format = new() { MaximumFractionDigits = 2 };

        public double? Value { get; set; } = 0;

        public int Renders { get; private set; }

        public List<double?> Commits { get; } = [];

        public void Rerender() => StateHasChanged();

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            Renders++;
            builder.OpenComponent<NumberFieldRoot>(0);
            builder.AddComponentParameter(1, nameof(NumberFieldRoot.Value), Value);
            builder.AddComponentParameter(2, nameof(NumberFieldRoot.Format), s_format);
            builder.AddComponentParameter(3, nameof(NumberFieldRoot.Culture), CultureInfo.GetCultureInfo("en-US"));
            builder.AddComponentParameter(4, nameof(NumberFieldRoot.OnValueChange), EventCallback.Factory.Create<ValueChangeEventArgs<double?>>(this, e => e.Details.Cancel()));
            builder.AddComponentParameter(5, nameof(NumberFieldRoot.OnValueCommitted), EventCallback.Factory.Create<ValueCommittedEventArgs<double?>>(this, e => Commits.Add(e.Value)));
            builder.AddComponentParameter(6, nameof(NumberFieldRoot.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<NumberFieldInput>(0);
                b.CloseComponent();
            }));
            builder.CloseComponent();
        }
    }
}
