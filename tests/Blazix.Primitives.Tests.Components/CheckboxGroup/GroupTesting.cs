using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// Drives one box of a <see cref="CheckboxGroupHost"/> as the <c>button</c> module would, by its <c>data-testid</c>: the
/// Server report of a click on its hidden input, or the WebAssembly decision inside it (the CheckboxTestContext model).
/// </summary>
internal static class GroupTesting
{
    /// <summary>Reports a toggle of the box's hidden input to the input's new value, after which the box reads what it shows.</summary>
    public static Task ToggleAsync<TComponent>(this CheckboxTestContext test, IRenderedComponent<TComponent> cut, string testId)
        where TComponent : IComponent
    {
        CheckboxRoot box = Box(cut, testId);
        ButtonTwin twin = Twin(test, box);
        bool next = cut.Find($"[data-testid={testId}]").GetAttribute("aria-checked") != "true";

        // A mixed box flips its underlying value, which its hidden input shows; the test reads the input's value.
        if (cut.Find($"[data-testid={testId}]").GetAttribute("aria-checked") == "mixed")
        {
            next = !box.InputChecked;
        }

        string payload = $$"""{"checked":{{(next ? "true" : "false")}},"event":{{CheckboxTestContext.InputClickJson()}}}""";
        return cut.InvokeAsync(() => twin.OnToggle(payload));
    }

    /// <summary>Asks the toggle decision of the box as the module does on WebAssembly, inside the input's click.</summary>
    public static Task<string> DecideToggleAsync<TComponent>(this CheckboxTestContext test, IRenderedComponent<TComponent> cut, string testId)
        where TComponent : IComponent
    {
        ButtonTwin twin = Twin(test, Box(cut, testId));
        return cut.InvokeAsync(() => twin.Decide(ButtonTwin.TogglePoint, CheckboxTestContext.InputClickJson()));
    }

    public static string? Checked<TComponent>(this IRenderedComponent<TComponent> cut, string testId)
        where TComponent : IComponent =>
        cut.Find($"[data-testid={testId}]").GetAttribute("aria-checked");

    public static CheckboxRoot Box<TComponent>(IRenderedComponent<TComponent> cut, string testId)
        where TComponent : IComponent =>
        ((IRenderedComponent<IComponent>)cut).FindComponents<CheckboxRoot>()
            .Single(c => c.Instance.AdditionalAttributes is { } attributes && Equals(attributes["data-testid"], testId))
            .Instance;

    private static ButtonTwin Twin(CheckboxTestContext test, CheckboxRoot box)
    {
        JSRuntimeInvocation attach = test.ButtonModule.Invocations["attach"].Last(i => Equals(i.Arguments[0], box.Element));
        return ((DotNetObjectReference<ButtonTwin>)attach.Arguments[2]!).Value;
    }
}
