using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Form: a submission that passes with no consumer callback, from the form module's report to C#'s
/// answer, measured on the renderer's dispatcher. It includes the report's deserialization, the state applied to the one
/// field, its validation and the answer's serialization. The budget was taken from the first measurement and only tightens.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class FormAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-23 (arm64, .NET 11 RC2, Release): 2248 bytes, most of it the report's deserialization, the
    // values object built for OnSubmit, the event details and the answer's JSON. 2256 since M2 track 6 (2026-10-23): the
    // registry entry's ResolveControlId reference, 8 bytes in the entry record a commit copies with its validity.
    private const long PassedSubmitBudgetBytes = 2256;

    private readonly FieldTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a passed submission with no consumer callback stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_passed_submission_with_no_consumer_callback_stays_within_its_budget()
    {
        IRenderedComponent<Form> cut = host.Context.Render<Form>(p => p
            .AddChildContent<FieldRoot>(f => f.Add(x => x.Name, "email").AddChildContent<FieldControl>(c => c.AddUnmatched("data-testid", "email"))));
        FormTwin twin = host.Module.FormElement.Twin!;
        string json = JsonSerializer.Serialize(
            new FormSubmitRequest
            {
                Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true },
                Controls = [new FormControlState { Id = cut.Find("[data-testid=email]").GetAttribute("id")!, Value = "a@b.co" }],
            },
            BlazixJsonContext.Default.FormSubmitRequest);
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnSubmitRequest(json));
        }

        long allocated = 0;
        await cut.InvokeAsync(() =>
        {
            Task<string>? answer = null;
            allocated = AllocationSampler.LeastOfFive(() => answer = twin.OnSubmitRequest(json));
            Assert.True(answer!.IsCompletedSuccessfully);
        });

        Assert.True(allocated <= PassedSubmitBudgetBytes, $"a passed submission allocated {allocated} bytes; the budget is {PassedSubmitBudgetBytes}");
    }
}
