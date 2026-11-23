using Bunit;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for OtpField (issue 278, M6 track 7; decision 26 (a), chosen by the user 2026-11-18): a re-render of a root
/// with unchanged parameters, which sends nothing to its modules, and one edit's C# path (the parse of the module's report,
/// normalization, the request, the commit and the render of six slots). Each figure is the least of five runs after a
/// warm-up (AllocationSampler's rule), measured on the test thread. The render count of one edit is measured by counting
/// ClassValue invocations (the lesson of issue 110).
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class OtpFieldAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-20 (arm64, .NET 11, Release, dynamic PGO off), the least of five, the same in three
    // processes: 2,584 bytes for the re-render of the root and its six slots, and 5,600 bytes for an edit, most of it the
    // source-generated parse of the report's JSON, the request's async state machines and the seven parts' new state
    // records. CI's Linux figure re-pins them (issue 304).
    private const long RerenderBudgetBytes = 2584;
    private const long EditBudgetBytes = 5600;

    private readonly OtpFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a one-time code field with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.DefaultValue, "12"));
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        int otpUpdates = test.OtpFieldHandle.Invocations["update"].Count;
        int formUpdates = test.FormHandle.Invocations["update"].Count;
        int compositeUpdates = test.CompositeHandle.Invocations["update"].Count;
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            cut.Render();
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(otpUpdates, test.OtpFieldHandle.Invocations["update"].Count);
        Assert.Equal(formUpdates, test.FormHandle.Invocations["update"].Count);
        Assert.Equal(compositeUpdates, test.CompositeHandle.Invocations["update"].Count);
        AllocationSampler.AssertWithinBudget(allocated, RerenderBudgetBytes, "a re-render");
    }

    [Fact(DisplayName = "one edit through normalization, the commit and the render of six slots stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task One_edit_stays_within_its_budget()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.DefaultValue, "12"));
        OtpFieldInteropProbe probe = new(test, cut);
        for (int i = 0; i < 20; i++)
        {
            await probe.EditAsync();
        }

        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task edited = probe.EditAsync();
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            await edited;
        }

        Assert.Equal(probe.Revision, test.OtpOptions[^1].Revision);
        AllocationSampler.AssertWithinBudget(allocated, EditBudgetBytes, "an edit");
    }

    // Each part's class function runs once per render of that part. The edit arrives outside an event dispatch, so each
    // render is its own batch: a slot renders once for its own subscription and once more because the root, which renders,
    // passes its child content again (the SliderRenderCountTests precedent). Measured 2026-11-20: the root 1, each slot 2.
    [Fact(DisplayName = "port: one edit renders the root once and each slot twice when every part has a state function")]
    [Trait("Requirement", "ADR16-7")]
    public async Task One_edit_renders_the_root_and_each_slot_once()
    {
        int rootRenders = 0;
        int slotRenders = 0;
        IRenderedComponent<OtpFieldRoot> cut = test.Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 6)
            .Add(r => r.DefaultValue, "12")
            .Add(r => r.ClassValue, (Func<OtpFieldRootState, string?>)(_ =>
            {
                rootRenders++;
                return null;
            }))
            .AddChildContent(builder =>
            {
                for (int i = 0; i < 6; i++)
                {
                    builder.OpenComponent<OtpFieldInput>(0);
                    builder.SetKey(i);
                    builder.AddAttribute(1, nameof(OtpFieldInput.ClassValue), (Func<OtpFieldInputState, string?>)(_ =>
                    {
                        slotRenders++;
                        return null;
                    }));
                    builder.CloseComponent();
                }
            }));
        rootRenders = 0;
        slotRenders = 0;

        await test.EditAsync(cut, 2, "input", "3", 1);

        Assert.Equal("123", test.OtpOptions[^1].Value);
        Assert.Equal((1, 12), (rootRenders, slotRenders));
    }

    // Alternates slot 2 between two digits, so every edit changes the value; each report carries the next revision.
    private sealed class OtpFieldInteropProbe(OtpFieldTestContext test, IRenderedComponent<OtpFieldRoot> cut)
    {
        private readonly Iodine.Headless.Interop.OtpFieldTwin twin = test.OtpTwin();
        private readonly string[] reports = [.. Enumerable.Range(1, 64).Select(r => OtpFieldTestContext.EditJson(2, "input", r % 2 == 0 ? "3" : "4", r))];

        public int Revision { get; private set; }

        public Task EditAsync()
        {
            string report = reports[Revision];
            Revision++;
            return cut.InvokeAsync(() => twin.OnEdit(report));
        }
    }
}
