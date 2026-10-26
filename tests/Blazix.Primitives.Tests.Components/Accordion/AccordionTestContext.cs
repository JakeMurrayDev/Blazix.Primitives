using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The Accordion test files' scenarios (one per Base UI test's markup).</summary>
public enum AccordionScenario
{
    /// <summary>One item of value <c>0</c> with a header, a trigger and a panel; every knob of the scenario component applies.</summary>
    OneItem,

    /// <summary>Two items of values <c>0</c> and <c>1</c>.</summary>
    TwoItems,

    /// <summary>Two items of string values, <c>one</c> and <c>two</c> by default.</summary>
    TwoStringItems,

    /// <summary>Two items without a value, identified by their generated ids.</summary>
    TwoGeneratedItems,

    /// <summary>A controlled root whose consumer applies a request unless it was canceled.</summary>
    ApplyingControlled,

    /// <summary>A root bound with <c>@bind-Value</c>.</summary>
    Bound,

    /// <summary>A multiple root whose item value is an array.</summary>
    ArrayItem,
}

/// <summary>
/// The bUnit context of the Accordion tests: the library's services, captured loggers, and bUnit's JS interop mock standing
/// in for the <c>collapsible</c> and <c>button</c> modules, so a test can read what the parts sent and drive the twins'
/// callbacks as the modules would (ADR 06 §1, twin contract tests).
/// </summary>
public sealed class AccordionTestContext : IDisposable
{
    public const string CollapsibleModulePath = "./_content/Blazix.Primitives/js/collapsible.js";
    public const string ButtonModulePath = "./_content/Blazix.Primitives/js/button.js";

    public AccordionTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<ILogger<AccordionRoot<int>>>(RootLogger);
        Context.Services.AddSingleton<ILogger<AccordionPanel<int>>>(PanelLogger);
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        CollapsibleModule = Context.JSInterop.SetupModule(CollapsibleModulePath);
        CollapsibleHandle = SetupHandle(CollapsibleModule);
        ButtonModule = Context.JSInterop.SetupModule(ButtonModulePath);
        ButtonHandle = SetupHandle(ButtonModule);
    }

    public BunitContext Context { get; } = new();

    public CapturingLogger<AccordionRoot<int>> RootLogger { get; } = new();

    public CapturingLogger<AccordionPanel<int>> PanelLogger { get; } = new();

    public BunitJSModuleInterop CollapsibleModule { get; }

    public BunitJSModuleInterop CollapsibleHandle { get; }

    public BunitJSModuleInterop ButtonModule { get; }

    public BunitJSModuleInterop ButtonHandle { get; }

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>Renders a scenario.</summary>
    public IRenderedComponent<AccordionScenarios> Render(AccordionScenario scenario, Action<ComponentParameterCollectionBuilder<AccordionScenarios>>? parameters = null) =>
        Context.Render<AccordionScenarios>(p =>
        {
            p.Add(x => x.Scenario, scenario);
            parameters?.Invoke(p);
        });

    /// <summary>The options JSON of every attach of a module, in order.</summary>
    public IReadOnlyList<string?> AttachOptions(BunitJSModuleInterop module) =>
        [.. module.Invocations["attach"].Select(invocation => invocation.Arguments[1] as string)];

    /// <summary>The <see cref="CollapsibleTwin"/> of every panel attach, in order.</summary>
    internal IReadOnlyList<CollapsibleTwin> PanelTwins() =>
        [.. CollapsibleModule.Invocations["attach"].Select(invocation => ((DotNetObjectReference<CollapsibleTwin>)invocation.Arguments[2]!).Value)];

    /// <summary>Ends every attached panel's exit as the module reports it (the convergent hide and the completion message).</summary>
    public void CompletePanelExits<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        foreach (CollapsibleTwin twin in PanelTwins())
        {
            cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));
        }
    }

    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}
