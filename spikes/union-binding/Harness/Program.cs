using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Probe;

var services = new ServiceCollection().AddLogging().BuildServiceProvider();
await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

Console.WriteLine("== Rendered cases (what each parameter received at runtime)");
var cases = typeof(ModalValue).Assembly.GetTypes()
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

Console.WriteLine();
Console.WriteLine("== Case E: what the compiler emits for a declared union");
Dump(typeof(RenderValue<CheckedState>));
Dump(typeof(ClassValue<CheckedState>));
Console.WriteLine();
Console.WriteLine("== Case D: the hand-written [Union] struct");
Dump(typeof(ModalValue));

Console.WriteLine();
Console.WriteLine("== Boxing observations");
ModalValue.ValueReads = 0;
long before = GC.GetAllocatedBytesForCurrentThread();
long count = 0;
for (int i = 0; i < 100_000; i++)
{
    ModalValue m = (i & 1) == 0;
    if (m is bool b && b) count++;
    if (ModalValue.TrapFocus is TrapFocusLiteral) count++;
}
long after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"ModalValue: 100k conversions + 200k matches allocated {after - before} bytes, Value getter read {ModalValue.ValueReads} times (count={count})");

before = GC.GetAllocatedBytesForCurrentThread();
Func<CheckedState, string?> fn = s => null;
for (int i = 0; i < 100_000; i++)
{
    ClassValue<CheckedState> c = fn;
    if (c is Func<CheckedState, string?>) count++;
}
after = GC.GetAllocatedBytesForCurrentThread();
Console.WriteLine($"ClassValue<CheckedState> (generated, reference cases): 100k conversions + matches allocated {after - before} bytes (count={count})");

static void Dump(Type t)
{
    const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    Console.WriteLine($"{t.Name}: {(t.IsValueType ? "struct" : "class")}, sealed={t.IsSealed}, readonly={t.GetCustomAttributesData().Any(a => a.AttributeType.Name == "IsReadOnlyAttribute")}");
    foreach (var a in t.GetCustomAttributesData().Where(a => a.AttributeType.Name is "UnionAttribute"))
        Console.WriteLine($"  attribute : {a.AttributeType.FullName}, {a.AttributeType.Assembly.GetName().Name}");
    foreach (var i in t.GetInterfaces())
        Console.WriteLine($"  interface : {i.FullName}");
    foreach (var f in t.GetFields(all))
        Console.WriteLine($"  field     : {f.FieldType.Name} {f.Name}");
    foreach (var p in t.GetProperties(all))
        Console.WriteLine($"  property  : {p.PropertyType.Name} {p.Name}");
    foreach (var c in t.GetConstructors(all))
        Console.WriteLine($"  ctor      : ({string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name))})");
    foreach (var m in t.GetMethods(all).Where(m => !m.IsSpecialName || m.Name.StartsWith("op_")))
        Console.WriteLine($"  method    : {m.Name}({string.Join(", ", m.GetParameters().Select(p => (p.IsOut ? "out " : "") + p.ParameterType.Name))})");
}
