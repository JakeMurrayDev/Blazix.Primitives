using System.Diagnostics;
using System.Globalization;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The list part measurement of ADR 14 (issues 5, 28, 25): the time and the allocation of one render of a menu's items, per
/// item, by form, around bUnit's synchronous render on the test thread. A run with <c>MENU_MEASURE_OUT</c> set appends the
/// figures to that file. The ceilings only keep the measurement honest: they are far above the figures ADR 16 records.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class MenuListMeasureTests : IDisposable
{
    private readonly BunitContext context = new();

    public MenuListMeasureTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Theory(DisplayName = "port: the render cost per item of a menu of 100 and 500 items, by form")]
    [InlineData(100, "plain")]
    [InlineData(100, "substitute")]
    [InlineData(100, "bag")]
    [InlineData(500, "plain")]
    [InlineData(500, "substitute")]
    [InlineData(500, "bag")]
    [Trait("Verifies", "ADR02-3")]
    [Trait("Verifies", "ADR16-1")]
    public void Measures_the_render_cost_per_item(int count, string form)
    {
        IRenderedComponent<MenuMeasureHost> cut = context.Render<MenuMeasureHost>(p => p
            .Add(x => x.Count, count)
            .Add(x => x.Form, form));
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long bytes = AllocationSampler.LeastOfFive(() => cut.Render());
        double best = double.MaxValue;
        for (int i = 0; i < 20; i++)
        {
            long start = Stopwatch.GetTimestamp();
            cut.Render();
            best = Math.Min(best, Stopwatch.GetElapsedTime(start).TotalMicroseconds);
        }

        double bytesPerItem = (double)bytes / count;
        double microsecondsPerItem = best / count;
        if (Environment.GetEnvironmentVariable("MENU_MEASURE_OUT") is { Length: > 0 } path)
        {
            File.AppendAllText(path, string.Create(CultureInfo.InvariantCulture, $"{count} {form}: {bytesPerItem:F0} B/item, {microsecondsPerItem:F1} us/item (best of 20)\n"));
        }

        Assert.True(bytesPerItem < 20_000, $"{bytesPerItem} bytes per item");
    }
}
