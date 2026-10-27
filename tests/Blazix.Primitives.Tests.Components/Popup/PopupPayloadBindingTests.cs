using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The payload probe's permanent evidence at L2 (issue 116; ADR 08 Amendments 2026-10-27): one Razor host binds both forms
/// of the generic-only popup parts, compiled by the Razor SDK as a consumer's markup is.
/// </summary>
public sealed class PopupPayloadBindingTests : IDisposable
{
    private readonly PopupTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the generic popup parts bind a plain popup through TPayload=\"NoPayload\" and infer a payload popup from its Handle")]
    [Trait("Issue", "116")]
    [Trait("Requirement", "PSH-API-07")]
    public async Task The_generic_popup_parts_bind_a_plain_popup_through_NoPayload_and_infer_a_payload_popup_from_its_Handle()
    {
        IRenderedComponent<PopupPayloadBindingHost> cut = test.Context.Render<PopupPayloadBindingHost>();

        // The plain popup: its Trigger inherited NoPayload and registered into the Root's store, which opened from DefaultOpen.
        Assert.NotNull(cut.Instance.PlainRoot);
        Assert.True(cut.Instance.PlainRoot!.Store.IsOpen);
        Assert.NotNull(cut.Instance.PlainRoot.Store.Triggers.GetById("plain-trigger"));
        Assert.Single(cut.FindComponents<TestPopupTrigger<NoPayload>>());

        // The payload popup: inferred from Handle, its Trigger's payload reaches the template's @context when it opens.
        Assert.Equal("No payload", cut.Find("[data-testid=\"payload\"]").TextContent);
        Assert.Single(cut.FindComponents<TestPopupTrigger<string>>());

        await cut.Find("#payload-trigger").ClickAsync(new());

        Assert.True(cut.Instance.PayloadRoot!.Store.IsOpen);
        Assert.Equal("first", cut.Find("[data-testid=\"payload\"]").TextContent);
    }
}
