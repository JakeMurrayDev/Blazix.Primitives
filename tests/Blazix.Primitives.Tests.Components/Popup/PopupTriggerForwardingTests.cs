using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// PSH-STA-28/30 (useTriggerDataForwarding, popupStoreUtils.ts:339-394): a trigger writes its forwarded state whenever it
/// comes to own the mounted popup, so a trigger that takes the popup back after another one owned it writes its payload
/// again. Found by M4 track 6 (Tooltip's hover hand-off, TIP-API-19); design point 13.8 of that track, pending the user.
/// </summary>
public sealed class PopupTriggerForwardingTests : IDisposable
{
    private readonly PopupTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a trigger that takes the popup back from another trigger writes its payload again")]
    [Trait("Requirement", "PSH-STA-28")]
    [Trait("Requirement", "PSH-STA-30")]
    public async Task A_trigger_that_takes_the_popup_back_writes_its_payload_again()
    {
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<TestPopupRoot<string>>(0);
            builder.AddComponentParameter(1, nameof(TestPopupRoot<string>.ChildContent), (RenderFragment)(content =>
            {
                for (int number = 1; number <= 2; number++)
                {
                    content.OpenComponent<TestPopupTrigger<string>>(0);
                    content.SetKey(number);
                    content.AddComponentParameter(1, "id", $"trigger-{number}");
                    content.AddComponentParameter(2, nameof(TestPopupTrigger<string>.Payload), number.ToString(System.Globalization.CultureInfo.InvariantCulture));
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

        await cut.Find("#trigger-1").ClickAsync(new());
        await cut.Find("#trigger-2").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("[data-testid=\"root-payload\"]").TextContent));

        await cut.Find("#trigger-1").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-testid=\"root-payload\"]").TextContent));
        Assert.True(cut.Find("#trigger-1").HasAttribute("data-popup-open"));
    }
}
