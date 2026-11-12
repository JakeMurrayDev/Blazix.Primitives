using System.Globalization;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A bUnit context for the Combobox tests: the library's services, every module loose, and the <c>combobox</c>,
/// <c>interactions</c> and <c>composite</c> attaches recorded, so a test types into the input, presses a trigger or moves the
/// highlight as the modules report them on Server (the twins' <c>[JSInvokable]</c> callbacks).
/// </summary>
internal sealed class ComboboxTestContext : IDisposable
{
    public const string ComboboxPath = "./_content/Blazix.Primitives/js/combobox.js";

    public const string InteractionsPath = "./_content/Blazix.Primitives/js/interactions.js";

    public const string CompositePath = "./_content/Blazix.Primitives/js/composite.js";

    public const string FormPath = "./_content/Blazix.Primitives/js/form.js";

    private const string InputMarker = "\"part\":\"input\"";

    private const string InputTemplate = """
        {"kind":"InputEvent","type":"input","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    private const string ClickTemplate = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    private const string KeyTemplate = """
        {"kind":"KeyboardEvent","type":"keydown","key":"{0}","code":"{0}","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":1}
        """;

    public ComboboxTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Combobox = Context.JSInterop.SetupModule(ComboboxPath);
        ComboboxHandle = Combobox.SetupModule("attach", _ => true);
        ComboboxHandle.SetupVoid("update", _ => true).SetVoidResult();
        ComboboxHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        Interactions = Context.JSInterop.SetupModule(InteractionsPath);
        InteractionsHandle = Interactions.SetupModule("attach", _ => true);
        InteractionsHandle.SetupVoid("update", _ => true).SetVoidResult();
        InteractionsHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        Composite = Context.JSInterop.SetupModule(CompositePath);
        CompositeHandle = Composite.SetupModule("attach", _ => true);
        CompositeHandle.SetupVoid("update", _ => true).SetVoidResult();
        CompositeHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        Form = Context.JSInterop.SetupModule(FormPath);
        FormHandle = Form.SetupModule("attach", _ => true);
        FormHandle.SetupVoid("update", _ => true).SetVoidResult();
        FormHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    public BunitContext Context { get; } = new();

    public BunitJSModuleInterop Combobox { get; }

    public BunitJSModuleInterop ComboboxHandle { get; }

    public BunitJSModuleInterop Interactions { get; }

    public BunitJSModuleInterop InteractionsHandle { get; }

    public BunitJSModuleInterop Composite { get; }

    public BunitJSModuleInterop CompositeHandle { get; }

    public BunitJSModuleInterop Form { get; }

    public BunitJSModuleInterop FormHandle { get; }

    /// <summary>Gets the snapshot JSON of a typed <c>input</c> event.</summary>
    public static string InputEvent => InputTemplate;

    /// <summary>Gets the snapshot JSON of a mouse click.</summary>
    public static string Click => ClickTemplate;

    /// <summary>Gets the snapshot JSON of a keydown.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The JSON.</returns>
    public static string Key(string key) => KeyTemplate.Replace("{0}", key, StringComparison.Ordinal);

    public void Dispose() => Context.Dispose();

    /// <summary>Gets the twin of the latest <c>combobox</c> handle of a part.</summary>
    /// <param name="part">The part literal, such as <c>input</c>.</param>
    public ComboboxTwin TwinOf(string part)
    {
        string marker = "\"part\":\"" + part + "\"";
        JSRuntimeInvocation attach = Combobox.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string options && options.Contains(marker, StringComparison.Ordinal));
        return ((DotNetObjectReference<ComboboxTwin>)attach.Arguments[2]!).Value;
    }

    /// <summary>Gets the options a part's <c>combobox</c> handle sent last.</summary>
    /// <param name="part">The part literal, such as <c>input-group</c>.</param>
    public string LastOptions(string part)
    {
        string marker = "\"part\":\"" + part + "\"";
        string? options = null;
        foreach (JSRuntimeInvocation attach in Combobox.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in ComboboxHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains(marker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    /// <summary>Gets the snapshot JSON of an event of another type and time than a template's.</summary>
    /// <param name="template">The template, such as <see cref="Click"/>.</param>
    /// <param name="type">The event's type.</param>
    /// <param name="timeStamp">The event's time stamp, which tells two events apart.</param>
    /// <returns>The JSON.</returns>
    public static string Event(string template, string type, int timeStamp) =>
        System.Text.RegularExpressions.Regex.Replace(template, "\"type\":\"[a-z]+\"", "\"type\":\"" + type + "\"")
            .Replace("\"timeStamp\":1", "\"timeStamp\":" + timeStamp.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>Gets the options the input's <c>combobox</c> handle sent last.</summary>
    public string LastInputOptions()
    {
        string? options = null;
        foreach (JSRuntimeInvocation attach in Combobox.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains(InputMarker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        foreach (JSRuntimeInvocation update in ComboboxHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains(InputMarker, StringComparison.Ordinal))
            {
                options = json;
            }
        }

        return options ?? string.Empty;
    }

    /// <summary>Types into the input as the module reports a typed change on Server.</summary>
    /// <param name="cut">The rendered component.</param>
    /// <param name="text">The input's text after the change.</param>
    /// <param name="typed">Whether the change is typed input.</param>
    public Task TypeAsync(IRenderedComponent<ComboboxHarness> cut, string text, bool typed = true)
    {
        string json = $$"""{"text":{{System.Text.Json.JsonSerializer.Serialize(text)}},"typed":{{(typed ? "true" : "false")}},"composing":false,"event":{{InputEvent}}}""";
        ComboboxTwin twin = TwinOf(ComboboxTwin.InputPart);
        return cut.InvokeAsync(() => twin.OnInputChange(json));
    }

    /// <summary>Gets the twin of the session handle (the attach whose target is <see langword="null"/>).</summary>
    public InteractionsTwin SessionTwin => (DotNetObjectReference<InteractionsTwin>)Interactions.Invocations["attach"].Last(attach => attach.Arguments[0] is null).Arguments[2]! is { } reference ? reference.Value : null!;

    /// <summary>Requests an open change through the session, as the module reports Escape or an outside press.</summary>
    public Task SessionRequestAsync(IRenderedComponent<ComboboxHarness> cut, bool open, string reason, string? eventJson = null)
    {
        string json = $$"""{"open":{{(open ? "true" : "false")}},"reason":"{{reason}}","event":{{eventJson ?? Click}},"triggerId":null}""";
        InteractionsTwin twin = SessionTwin;
        return cut.InvokeAsync(() => twin.OnRequest(json));
    }

    /// <summary>Gets the snapshot JSON of a value, culture-invariant.</summary>
    public static string Number(double value) => value.ToString(CultureInfo.InvariantCulture);
}
