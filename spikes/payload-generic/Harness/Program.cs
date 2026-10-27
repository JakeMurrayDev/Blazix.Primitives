using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Probe.Parts;

var services = new ServiceCollection().AddLogging().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

Console.WriteLine("== Rendered cases (which component each tag resolved to, and what it received)");
var cases = typeof(PopHandle<>).Assembly.GetTypes()
    .Where(t => t.Namespace == "Probe.Cases" && typeof(IComponent).IsAssignableFrom(t))
    .OrderBy(t => t.Name, StringComparer.Ordinal);
foreach (var type in cases)
{
    try
    {
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync(type);
            return output.ToHtmlString();
        });
        Console.WriteLine($"{type.Name,-4} {html.Trim()}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{type.Name,-4} THREW {ex.GetType().Name}: {ex.Message}");
    }
}
