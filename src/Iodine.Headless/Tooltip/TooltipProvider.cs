using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// Base UI's Tooltip.Provider (tooltip/provider/TooltipProvider.tsx; TIP-STR-01, TIP-API-01..03, TIP-STA-14). Renders no
// element (the DirectionProvider precedent): it draws a provider token (PartIdGenerator, never rendered), cascades its
// fixed context, and attaches the delay group by token through the `interactions` module's provider role (ADR 04
// Amendments 2026-10-27, chosen by the user 2026-10-27). Every group timer is JavaScript's; a member's open closes the
// previous member through that tooltip's session with reason `none` (TIP-DIS-04, FLT-EVT-07).
// The provider's open delay is not sent to the group: Tooltip resolves the open delay per trigger, reading the group only
// for its instant phase (TooltipTrigger.tsx:113-119), and a group seeded with the provider's 0 would read as instant and
// override a trigger's own delay outside the instant phase (TIP-API-15, "respects a trigger delay over delay=0 outside
// the instant phase").
/// <summary>
/// Groups the tooltips inside it: once one has opened, hovering another opens it at once, with no delay and no
/// transition, until <see cref="Timeout"/> after the last one closed. Base UI's <c>Tooltip.Provider</c>; it renders no
/// element.
/// </summary>
public sealed class TooltipProvider : ComponentBase, IAsyncDisposable
{
    private InteractionsProviderBehavior? behavior;
    private TooltipProviderContext? context;
    private RenderFragment? cascadedContent;
    private RenderFragment? cascade;

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private PartIdGenerator Ids { get; set; } = null!;

    /// <summary>Gets or sets how long to wait before opening a tooltip on hover.</summary>
    /// <value>
    /// The delay used by every <see cref="TooltipTrigger{TPayload}"/> inside that has no <c>Delay</c> of its own, or
    /// <see langword="null"/> (the default) for 600 milliseconds.
    /// </value>
    [Parameter]
    public Duration? Delay { get; set; }

    /// <summary>Gets or sets how long to wait before closing a tooltip after the pointer leaves its trigger.</summary>
    /// <value>
    /// The delay used by every <see cref="TooltipTrigger{TPayload}"/> inside that has no <c>CloseDelay</c> of its own, or
    /// <see langword="null"/> (the default) for none. A change applies to the next close.
    /// </value>
    [Parameter]
    public Duration? CloseDelay { get; set; }

    /// <summary>Gets or sets how long after a tooltip closed another one still opens at once.</summary>
    /// <value>The grace period, or <see langword="null"/> (the default) for 400 milliseconds.</value>
    [Parameter]
    public Duration? Timeout { get; set; }

    /// <summary>Gets or sets the content.</summary>
    /// <value>The child content, or <see langword="null"/>.</value>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): the group exists before a hover reads it.
    /// <summary>Gets a task that completes when the provider's delay group is attached.</summary>
    internal Task TwinAttached => behavior?.TwinAttached ?? Task.CompletedTask;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        behavior = new InteractionsProviderBehavior(JSRuntime, Ids);
        context = new TooltipProviderContext(behavior.Token);
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        // The triggers subscribed to the fixed cascade re-render now, in the batch that applies the new delays.
        context!.SetDelays(TooltipDelays.Milliseconds(Delay), TooltipDelays.Milliseconds(CloseDelay));
        behavior!.DelayClose = TooltipDelays.Milliseconds(CloseDelay);
        behavior.Timeout = TooltipDelays.Timeout(Timeout);
        if (cascade is null || !ReferenceEquals(cascadedContent, ChildContent))
        {
            cascadedContent = ChildContent;
            cascade = context.Cascade(cascadedContent);
        }
    }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, cascade);

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => behavior!.AfterRenderAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (behavior is not null)
        {
            await behavior.DisposeAsync();
        }
    }
}
