using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the field: a keystroke on an uncontrolled control after the first, from the form module's report to
/// the end of its handling, measured on the renderer's dispatcher. The report's deserialization is part of the cost
/// (it is what every keystroke pays); the parts render nothing, since the field's state is unchanged after the first
/// keystroke. The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class FieldAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-22 (arm64, .NET 11 RC2, Release): 1024 bytes, most of it the report's deserialization (the
    // report, its validity and event snapshot, the value string) and the handler's task.
    private const long KeystrokeBudgetBytes = 1024;

    private readonly FieldTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a keystroke on an uncontrolled control after the first stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_keystroke_on_an_uncontrolled_control_after_the_first_stays_within_its_budget()
    {
        IRenderedComponent<FieldRoot> cut = host.Context.Render<FieldRoot>(p => p
            .AddChildContent<FieldLabel>(l => l.AddChildContent("Name"))
            .AddChildContent<FieldControl>());
        FormTwin twin = host.Module.Control.Twin!;
        string[] reports = [.. Enumerable.Range(1, 30).Select(i => Report(new string('a', i)))];
        for (int i = 0; i < 29; i++)
        {
            string json = reports[i];
            cut.InvokeAsync(() => twin.OnControlReport(json));
        }

        long allocated = 0;
        string last = reports[29];
        cut.InvokeAsync(() =>
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            twin.OnControlReport(last);
            allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.True(allocated <= KeystrokeBudgetBytes, $"a keystroke allocated {allocated} bytes; the budget is {KeystrokeBudgetBytes}");
    }

    private static string Report(string value) => JsonSerializer.Serialize(
        new FormControlReport
        {
            Type = "input",
            Value = value,
            Validity = NativeValidity.Default,
            Event = new EventSnapshot { Kind = EventKind.InputEvent, Type = "input", Target = new EventTargetInfo { TagName = "INPUT" } },
        },
        BlazixJsonContext.Default.FormControlReport);
}
