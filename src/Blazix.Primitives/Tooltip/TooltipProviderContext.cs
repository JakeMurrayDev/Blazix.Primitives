using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// tooltip/provider/TooltipProviderContext.ts:7-11 (Base UI's context holds the provider's `delay`; the close delay goes
// through FloatingDelayGroup). Cascaded fixed by TooltipProvider (ADR 16 rule 9): its triggers resolve their delays in C#
// (issue 121: `Delay ?? Provider ?? 600`, `CloseDelay ?? Provider ?? 0`) and are told of a change through the listeners,
// so a later CloseDelay applies to the next close (TIP-API-02). The token names the provider's delay group in the
// `interactions` module (ADR 04 Amendments 2026-10-27); it is never rendered.
/// <summary>The delays and the delay-group token a tooltip provider gives the triggers inside it.</summary>
internal sealed class TooltipProviderContext(string token)
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets the token of the provider's delay group.</summary>
    public string Token { get; } = token;

    /// <summary>Gets the provider's open delay in milliseconds, or <see langword="null"/> when not supplied.</summary>
    public int? Delay { get; private set; }

    /// <summary>Gets the provider's close delay in milliseconds, or <see langword="null"/> when not supplied.</summary>
    public int? CloseDelay { get; private set; }

    /// <summary>Records the provider's delays and tells the triggers when one changed.</summary>
    /// <param name="delay">The open delay in milliseconds, or <see langword="null"/>.</param>
    /// <param name="closeDelay">The close delay in milliseconds, or <see langword="null"/>.</param>
    public void SetDelays(int? delay, int? closeDelay)
    {
        if (Delay == delay && CloseDelay == closeDelay)
        {
            return;
        }

        Delay = delay;
        CloseDelay = closeDelay;

        // A listener may remove itself or another: iterating backwards with a bounds check never skips one still there.
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (i < listeners.Count)
            {
                listeners[i]();
            }
        }
    }

    /// <summary>Adds a listener called when a delay changed.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    internal RenderFragment Cascade(RenderFragment? content) => builder =>
    {
        builder.OpenComponent<CascadingValue<TooltipProviderContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<TooltipProviderContext>.Value), this);
        builder.AddComponentParameter(2, nameof(CascadingValue<TooltipProviderContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<TooltipProviderContext>.ChildContent), content);
        builder.CloseComponent();
    };
}

// TIP-API-01/02/03, TIP-API-15/16 (TooltipTrigger.tsx:78, 113-119, 152-157; tooltip/utils/constants.ts:1;
// TooltipProvider.tsx:13); ADR 08 Amendments 2026-10-27 (`null` means not supplied; chosen by the user 2026-10-27). The
// fallbacks are resolved in C# before they cross (issue 121).
/// <summary>Resolves a tooltip's millisecond parameters against their fallbacks.</summary>
internal static class TooltipDelays
{
    /// <summary>The open delay of a trigger with no delay inside no provider with one, in milliseconds.</summary>
    public const int OpenDelay = 600;

    /// <summary>The provider's timeout when not supplied, in milliseconds.</summary>
    public const int ProviderTimeout = 400;

    /// <summary>Resolves a trigger's hover open delay: its own, else its provider's, else 600 ms.</summary>
    /// <param name="trigger">The trigger's delay.</param>
    /// <param name="provider">The enclosing provider, or <see langword="null"/>.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static int Open(Duration? trigger, TooltipProviderContext? provider) => Milliseconds(trigger) ?? provider?.Delay ?? OpenDelay;

    /// <summary>Resolves a trigger's hover close delay: its own, else its provider's, else 0.</summary>
    /// <param name="trigger">The trigger's close delay.</param>
    /// <param name="provider">The enclosing provider, or <see langword="null"/>.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static int Close(Duration? trigger, TooltipProviderContext? provider) => Milliseconds(trigger) ?? provider?.CloseDelay ?? 0;

    // TIP-API-16, TIP §16 Q16 (decided 2026-10-18: replicated): the store's closeDelay, which the popup's own leave reads,
    // has no provider fallback.
    /// <summary>Resolves the close delay a trigger forwards to the store: its own, else 0.</summary>
    /// <param name="trigger">The trigger's close delay.</param>
    /// <returns>The delay in milliseconds.</returns>
    public static int Forwarded(Duration? trigger) => Milliseconds(trigger) ?? 0;

    /// <summary>Resolves the provider's timeout: its own, else 400 ms.</summary>
    /// <param name="timeout">The provider's timeout.</param>
    /// <returns>The timeout in milliseconds.</returns>
    public static int Timeout(Duration? timeout) => Milliseconds(timeout) ?? ProviderTimeout;

    /// <summary>Returns a duration in whole milliseconds.</summary>
    /// <param name="duration">The duration.</param>
    /// <returns>The milliseconds, rounded, or <see langword="null"/> when not supplied.</returns>
    public static int? Milliseconds(Duration? duration) => duration is { } value ? (int)Math.Round(value.ToMilliseconds(), MidpointRounding.AwayFromZero) : null;
}
