using Bunit;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// Render counts of the ScrollArea parts on the module's reports (SCR-STA-20; ADR 16 rules 8 and 9; the standing rule that
/// render-count claims are measured, by counting <c>ClassValue</c> invocations): a report equal to the stored state renders
/// nothing, a scrolling flip renders only the parts whose state function reads it, and an edge crossing renders each part once.
/// </summary>
public sealed class ScrollAreaRenderCountTests : IDisposable
{
    private readonly ScrollAreaTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a measurement equal to the stored state renders no part, and an edge crossing renders each part once")]
    [Trait("Requirement", "SCR-STA-20")]
    public async Task A_measurement_equal_to_the_stored_state_renders_no_part()
    {
        Dictionary<string, int> renders = new(StringComparer.Ordinal);
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.RootClass, _ => Count(renders, "root"))
            .Add(x => x.ViewportClass, _ => Count(renders, "viewport"))
            .Add(x => x.ContentClass, _ => Count(renders, "content"))
            .Add(x => x.ScrollbarClass, _ => Count(renders, "scrollbar"))
            .Add(x => x.ThumbClass, _ => Count(renders, "thumb")));
        await test.OverflowAsync(cut, hasY: true, yEnd: true);
        renders.Clear();

        // The module sends a report only on a change; an equal one, were it sent, renders nothing.
        await test.OverflowAsync(cut, hasY: true, yEnd: true);
        Assert.Empty(renders);

        // The root renders first and its subtree with it, in one batch: the thumb, which projects no overflow attribute, renders
        // once because its scrollbar's content did; no part renders twice.
        await test.OverflowAsync(cut, hasY: true, yStart: true, yEnd: true);
        Assert.Equal(new Dictionary<string, int> { ["root"] = 1, ["viewport"] = 1, ["content"] = 1, ["scrollbar"] = 1, ["thumb"] = 1 }, renders);
    }

    [Fact(DisplayName = "port: a scrolling flip renders only the parts whose state function reads it")]
    [Trait("Requirement", "SCR-STA-20")]
    [Trait("Requirement", "SCR-STA-01")]
    public async Task A_scrolling_flip_renders_only_the_parts_with_a_state_function()
    {
        Dictionary<string, int> renders = new(StringComparer.Ordinal);
        IRenderedComponent<ScrollAreaHost> cut = test.Context.Render<ScrollAreaHost>(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.ThumbClass, _ => Count(renders, "thumb")));
        await test.OverflowAsync(cut, hasY: true, yEnd: true);
        renders.Clear();

        await test.ScrollingAsync(cut, x: false, y: true);

        // The vertical thumb's state function reads the flag; the horizontal thumb's reads its own axis, which did not flip.
        Assert.Equal(new Dictionary<string, int> { ["thumb"] = 1 }, renders);
    }

    private static string? Count(Dictionary<string, int> renders, string part)
    {
        renders[part] = renders.GetValueOrDefault(part) + 1;
        return null;
    }
}
