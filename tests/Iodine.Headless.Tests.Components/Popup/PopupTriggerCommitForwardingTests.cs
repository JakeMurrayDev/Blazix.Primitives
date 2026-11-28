using Bunit;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// PSH-STA-30/31 (useTriggerDataForwarding, popupStoreUtils.ts:339-394, a layout effect): a trigger's forwarded state is
/// written in the commit that makes it own the mounted popup, and a change of its values in the commit that renders them,
/// so no render of the trigger falls between the change and the payload write. Issue 142 (design point 13.10 of M4 track
/// 6, settled by the orchestrator 2026-10-28): the port forwarded from the trigger's <c>OnAfterRender</c>, one batch late,
/// which on Server is a round trip (the batch's acknowledgement) before the write and another before its render.
/// </summary>
public sealed class PopupTriggerCommitForwardingTests : IDisposable
{
    private readonly PopupTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a hand-off writes the new trigger's payload before the new trigger renders (issue 142)")]
    [Trait("Requirement", "PSH-STA-30")]
    [Trait("Requirement", "PSH-STA-31")]
    public async Task A_hand_off_writes_the_payload_before_the_new_trigger_renders()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTwoTriggers();
        await cut.Find("#trigger-1").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"root-payload\"]").TextContent));
        TestPopupStore<string> store = cut.FindComponent<TestPopupRoot<string>>().Instance.Store;
        TestPopupTrigger<string> second = cut.FindComponents<TestPopupTrigger<string>>()[1].Instance;
        int? rendersAtOwnership = null;
        int? rendersAtPayload = null;
        using IDisposable subscription = store.Subscribe(state =>
        {
            if (rendersAtOwnership is null && state.Popup.EffectiveActiveTriggerId == "trigger-2")
            {
                rendersAtOwnership = second.Renders;
            }

            if (rendersAtPayload is null && store.Payload == "2")
            {
                rendersAtPayload = second.Renders;
            }
        });

        await cut.Find("#trigger-2").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"root-payload\"]").TextContent));
        Assert.NotNull(rendersAtOwnership);
        Assert.Equal(rendersAtOwnership, rendersAtPayload);
    }

    [Fact(DisplayName = "port: a change of the owner's payload is written before the owner renders it (issue 142)")]
    [Trait("Requirement", "PSH-STA-30")]
    public async Task A_change_of_the_owners_payload_is_written_before_the_owner_renders_it()
    {
        IRenderedComponent<PopupTestHost> cut = RenderTwoTriggers();
        await cut.Find("#trigger-1").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"root-payload\"]").TextContent));
        TestPopupStore<string> store = cut.FindComponent<TestPopupRoot<string>>().Instance.Store;
        TestPopupTrigger<string> first = cut.FindComponents<TestPopupTrigger<string>>()[0].Instance;
        int rendersBefore = first.Renders;
        int? rendersAtPayload = null;
        using IDisposable subscription = store.Subscribe(_ =>
        {
            if (rendersAtPayload is null && store.Payload == "1b")
            {
                rendersAtPayload = first.Renders;
            }
        });

        await cut.Instance.SetAsync("payload-1", "1b");

        cut.WaitForAssertion(() => Assert.Equal("1b", cut.Find("[data-testid=\"root-payload\"]").TextContent));
        Assert.Equal(rendersBefore, rendersAtPayload);
    }

    private IRenderedComponent<PopupTestHost> RenderTwoTriggers() => test.Render((builder, host) =>
    {
        builder.OpenComponent<TestPopupRoot<string>>(0);
        builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
        {
            for (int number = 1; number <= 2; number++)
            {
                string fallback = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                content.OpenComponent<TestPopupTrigger<string>>(0);
                content.SetKey(number);
                content.AddComponentParameter(1, "id", $"trigger-{number}");
                content.AddComponentParameter(2, nameof(TestPopupTrigger<string>.Payload), host.Get<string>($"payload-{number}") ?? fallback);
                content.CloseComponent();
            }
        }));
        builder.AddComponentParameter(2, nameof(TestPopupRoot<string>.PayloadContent), (RenderFragment<string?>)(payload => content =>
        {
            content.OpenElement(0, "span");
            content.AddAttribute(1, "data-testid", "root-payload");
            content.AddContent(2, payload ?? "No payload");
            content.CloseElement();
        }));
        builder.CloseComponent();
    });
}
