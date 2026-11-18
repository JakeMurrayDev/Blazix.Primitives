// Renders every case of the probe through HtmlRenderer and prints what each part received, or the exception a parameter
// set threw; then the equality checks the brief asks for (allocation of the union's element-wise equality; an equal
// snap-point list read back; "1" against 1).
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Probe.Parts;

ServiceCollection services = new();
services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
await using ServiceProvider provider = services.BuildServiceProvider();
await using HtmlRenderer renderer = new(provider, provider.GetRequiredService<ILoggerFactory>());

Type[] cases = [.. typeof(Probe.TypeName).Assembly.GetTypes()
    .Where(type => type.Namespace == "Probe.Cases" && typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract)
    .OrderBy(type => type.Name, StringComparer.Ordinal)];

foreach (Type type in cases)
{
    string output = await renderer.Dispatcher.InvokeAsync(async () =>
    {
        try
        {
            HtmlRootComponent root = await renderer.RenderComponentAsync(type, ParameterView.Empty);
            return root.ToHtmlString();
        }
        catch (Exception exception)
        {
            return $"threw {exception.GetType().Name}: {exception.Message}";
        }
    });
    Console.WriteLine($"{type.Name}: {output.ReplaceLineEndings(" ")}");
}

// Equality checks.
SliderValues a = new double[] { 25, 45 };
SliderValues b = new List<double> { 25, 45 };
SliderValues c = 25;
bool warm = a == b && a != c;
long before = GC.GetAllocatedBytesForCurrentThread();
bool equal = false;
for (int i = 0; i < 1000; i++)
{
    equal = a == b && a != c && c == 25;
}

long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
Console.WriteLine($"equality: [25, 45] array == list: {a == b}; [25, 45] == 25: {a == c}; 1000 comparisons allocated {allocated} B (warm {warm}, {equal})");

IReadOnlyList<SnapPointValue> first = [0.5, "200px", 1];
IReadOnlyList<SnapPointValue> second = new List<SnapPointValue> { 0.5, "200px", 1 };
Console.WriteLine($"snap points: equal list by sequence: {first.SequenceEqual(second)}; by reference: {ReferenceEquals(first, second)}; \"1\" == 1: {new SnapPointValue("1") == new SnapPointValue(1)}");
