using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace: an `Avatar` namespace would read as a family type.
namespace Blazix.Primitives.Tests;

/// <summary>
/// The Avatar fallback's delay at L1 (AVA-ANI-07, AVA-ANI-08, AVA-EDGE-05; ADR 06 §1: C# timers under a
/// <see cref="FakeTimeProvider"/>). Port-marked: the titles are <c>AvatarFallback.test.tsx</c>'s where the behavior is
/// pure timer logic, applied to the timer the part owns; the part's rendering is tested at L2 under the same titles.
/// </summary>
public sealed class AvatarFallbackDelayTests
{
    private readonly FakeTimeProvider clock = new();
    private int elapsedCalls;

    [Fact(DisplayName = "shows the fallback when the delay has elapsed")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Shows_the_fallback_when_the_delay_has_elapsed()
    {
        using AvatarFallbackDelay delay = Create(100);
        Assert.False(delay.Restart(100));

        clock.Advance(TimeSpan.FromMilliseconds(99));
        Assert.Equal(0, elapsedCalls);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, elapsedCalls);
        Assert.True(delay.MarkPassed());
        Assert.True(delay.Passed);
    }

    [Fact(DisplayName = "keeps the fallback visible when delay changes from undefined to a number")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Keeps_the_fallback_visible_when_delay_changes_from_undefined_to_a_number()
    {
        using AvatarFallbackDelay delay = Create(0);
        Assert.True(delay.Passed);

        Assert.False(delay.Restart(0));
        Assert.False(delay.Restart(100));
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.True(delay.Passed);
        Assert.Equal(0, elapsedCalls);
    }

    [Fact(DisplayName = "restarts a pending delay when it changes")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Restarts_a_pending_delay_when_it_changes()
    {
        using AvatarFallbackDelay delay = Create(100);
        delay.Restart(100);
        clock.Advance(TimeSpan.FromMilliseconds(60));

        delay.Restart(100);
        clock.Advance(TimeSpan.FromMilliseconds(60));
        Assert.Equal(0, elapsedCalls);

        clock.Advance(TimeSpan.FromMilliseconds(40));
        Assert.Equal(1, elapsedCalls);
    }

    [Fact(DisplayName = "marks a negative delay as elapsed after the first render without a timer")]
    [Trait("Requirement", "AVA-EDGE-05")]
    public void Marks_a_negative_delay_as_elapsed_after_the_first_render_without_a_timer()
    {
        using AvatarFallbackDelay delay = Create(-1);
        Assert.False(delay.Passed);

        Assert.True(delay.Restart(-1));
        Assert.True(delay.Passed);
        Assert.Equal(0, elapsedCalls);
    }

    [Fact(DisplayName = "cancels the pending timer on dispose")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Cancels_the_pending_timer_on_dispose()
    {
        AvatarFallbackDelay delay = Create(100);
        delay.Restart(100);

        delay.Dispose();
        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, elapsedCalls);
    }

    private AvatarFallbackDelay Create(int delay) => new(clock, delay, () => elapsedCalls++);
}
