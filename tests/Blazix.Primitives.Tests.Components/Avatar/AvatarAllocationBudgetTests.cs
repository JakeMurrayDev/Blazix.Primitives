using Blazix.Primitives.Interop;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 at L2 for the Avatar image: a status message that changes nothing (the module reports the status the
/// image already has, as after a re-derivation) allocates only its payload, and renders nothing. The budget is the first
/// measurement and only tightens afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class AvatarAllocationBudgetTests : IDisposable
{
    private const string Payload = """{"status":"loaded"}""";

    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 120 bytes, all of it the twin's deserialization of the
    // payload (the record, its string and the reader's state); a twin with no part subscribed measures the same.
    private const long ReportBudgetBytes = 120;

    private readonly AvatarInterop interop = new();

    /// <inheritdoc />
    public void Dispose() => interop.Dispose();

    [Fact(DisplayName = "an image status message that changes nothing allocates only its payload")]
    [Trait("Requirement", "ADR16-7")]
    public void An_image_status_message_that_changes_nothing_allocates_only_its_payload()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "a.png"));
        interop.ReportImage(cut, "loaded");
        DomTwin twin = AvatarInterop.Twin<DomTwin>(interop.DomAttaches);
        IRenderedComponent<AvatarImage> image = cut.FindComponent<AvatarImage>();
        int renders = image.RenderCount;

        for (int i = 0; i < 20; i++)
        {
            twin.OnImageStatus(Payload);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        twin.OnImageStatus(Payload);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long payload = PayloadBytes();

        Assert.True(allocated <= ReportBudgetBytes, $"a status message allocated {allocated} bytes; the budget is {ReportBudgetBytes}");
        Assert.True(allocated <= payload, $"a status message allocated {allocated} bytes; its payload alone allocates {payload}");
        Assert.Equal(renders, image.RenderCount);
    }

    // The same message to a twin no part listens to: what the payload costs by itself.
    private long PayloadBytes()
    {
        DomTwin bare = new(interop.Context.JSInterop.JSRuntime);
        for (int i = 0; i < 20; i++)
        {
            bare.OnImageStatus(Payload);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        bare.OnImageStatus(Payload);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
