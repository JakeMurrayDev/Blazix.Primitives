// Renders every case of the probe through HtmlRenderer and prints what each root and item received (TValue, the supplied
// triple, the expressions, the item's supplied flag), or the exception a parameter set threw.
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
