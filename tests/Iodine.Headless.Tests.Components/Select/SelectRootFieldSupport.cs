using System.Text.Json;
using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// What the Field, Form and validation titles of <c>SelectRootFieldTests</c> need beyond <see cref="SelectTestBase"/>: the
/// <c>form</c> module's reports for the root's hidden input and the enclosing form, as the module makes them on Server.
/// The hidden input's native validity is derived from the markup C# rendered and the value it sent, the way
/// the browser reads it: <c>required</c> with an empty value is <c>valueMissing</c>.
/// </summary>
internal static class SelectRootFieldSupport
{
    private const string HiddenInput = "input[aria-hidden=\"true\"]";

    private const string TriggerPart = "\"part\":\"trigger\"";

    private const string TransitionsPath = "./_content/Blazix.Primitives/js/transitions.js";

    /// <summary>Gets the options of the root's hidden input as the <c>form</c> module last received them, or <see langword="null"/>.</summary>
    public static FormControlOptions? InputOptions(this SelectTestBase test)
    {
        FormControlOptions? latest = null;
        foreach (JSRuntimeInvocation attach in test.Form.Invocations["attach"])
        {
            if (ParseInput((string)attach.Arguments[1]!) is { } options)
            {
                latest = options;
            }
        }

        foreach (JSRuntimeInvocation update in test.FormHandle.Invocations["update"])
        {
            if (ParseInput((string)update.Arguments[0]!) is { } options)
            {
                latest = options;
            }
        }

        return latest;
    }

    /// <summary>Records the attaches of the <c>transitions</c> module, which <see cref="LastTransitionsTwin"/> reads; call it before rendering and take the twin of a field error right after it mounts.</summary>
    /// <param name="test">The test.</param>
    /// <returns>The recorded module.</returns>
    public static BunitJSModuleInterop RecordTransitions(this SelectTestBase test)
    {
        BunitJSModuleInterop module = test.JSInterop.SetupModule(TransitionsPath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return module;
    }

    /// <summary>Gets the <c>transitions</c> twin attached last, such as the one of a field error that was just mounted.</summary>
    /// <param name="test">The test.</param>
    /// <param name="transitions">The module <see cref="RecordTransitions"/> returned.</param>
    /// <returns>The twin.</returns>
    public static TransitionsTwin LastTransitionsTwin(this SelectTestBase test, BunitJSModuleInterop transitions) =>
        ((DotNetObjectReference<TransitionsTwin>)transitions.Invocations["attach"][^1].Arguments[2]!).Value;

    /// <summary>Reports that the exit of the element a <c>transitions</c> twin watches finished, as the module does (a field error stays mounted until then).</summary>
    /// <param name="test">The test.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="twin">The twin of the element.</param>
    public static Task CompleteExitAsync(this SelectTestBase test, IRenderedComponent<SelectHost> cut, TransitionsTwin twin) =>
        cut.InvokeAsync(() => twin.OnTransitionComplete("""{"open":false}"""));

    /// <summary>Focuses the trigger, as the browser's <c>focus</c> event reaches the part.</summary>
    public static Task FocusTriggerAsync(this SelectTestBase test, IRenderedComponent<SelectHost> cut) =>
        SelectTestBase.Trigger(cut).FocusAsync(new FocusEventArgs());

    /// <summary>Reports the trigger's blur as the <c>select</c> module does, then answers the refresh a commit starts.</summary>
    /// <param name="test">The test.</param>
    /// <param name="cut">The rendered host.</param>
    /// <param name="intoPopup">Whether the related target is inside the positioner.</param>
    public static async Task BlurTriggerAsync(this SelectTestBase test, IRenderedComponent<SelectHost> cut, bool intoPopup = false)
    {
        SelectTwin twin = test.SelectTwinOf(TriggerPart)!;
        string json = JsonSerializer.Serialize(new SelectTriggerBlur(intoPopup), BlazixJsonContext.Default.SelectTriggerBlur);
        await cut.InvokeAsync(() => twin.OnTriggerBlur(json));
        await test.SettleFormAsync(cut);
    }

    /// <summary>
    /// Answers the pending refresh of the root's hidden input with its native validity, as the module does, then lets the
    /// work that waited for it run on the renderer's dispatcher; repeated for the refreshes that work starts.
    /// </summary>
    public static async Task SettleFormAsync(this SelectTestBase test, IRenderedComponent<SelectHost> cut)
    {
        for (int round = 0; round < 3; round++)
        {
            if (test.InputOptions() is not { } options)
            {
                return;
            }

            FormControlReport report = new() { Type = "sync", Validity = ValidityOf(cut, options), Revision = options.Revision };
            string json = JsonSerializer.Serialize(report, BlazixJsonContext.Default.FormControlReport);
            FormTwin twin = test.InputTwin();
            await cut.InvokeAsync(() => twin.OnControlReport(json));
            await cut.InvokeAsync(static () => { });
        }
    }

    /// <summary>
    /// Reports a native submission of the enclosing form, as the module does: the state it reads from the root's hidden
    /// input, enrolled on the form, and the form's answer.
    /// </summary>
    public static async Task<FormSubmitAnswer> SubmitFormAsync(this SelectTestBase test, IRenderedComponent<SelectHost> cut)
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"]
            .Last(i => JsonSerializer.Deserialize((string)i.Arguments[1]!, BlazixJsonContext.Default.FormOptions)!.Submit is not null);
        FormTwin twin = ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
        FormControlOptions options = test.InputOptions()!;
        IElement input = cut.Find(HiddenInput);
        FormSubmitRequest request = new()
        {
            Event = new EventSnapshot { Kind = EventKind.Event, Type = "submit", DefaultPrevented = true, Target = new EventTargetInfo { TagName = "FORM" } },
            Controls = [new FormControlState { Id = input.GetAttribute("id") ?? string.Empty, Value = options.Value, Validity = ValidityOf(cut, options) }],
        };
        string json = JsonSerializer.Serialize(request, BlazixJsonContext.Default.FormSubmitRequest);
        string answer = await cut.InvokeAsync(() => twin.OnSubmitRequest(json));
        return JsonSerializer.Deserialize(answer, BlazixJsonContext.Default.FormSubmitAnswer)!;
    }

    private static FormTwin InputTwin(this SelectTestBase test)
    {
        JSRuntimeInvocation attach = test.Form.Invocations["attach"].Last(i => ParseInput((string)i.Arguments[1]!) is not null);
        return ((DotNetObjectReference<FormTwin>)attach.Arguments[2]!).Value;
    }

    // The root's hidden input is the control attached without event reports.
    private static FormControlOptions? ParseInput(string json) =>
        JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!.Control is { ReportEvents: false } control ? control : null;

    private static NativeValidity ValidityOf(IRenderedComponent<SelectHost> cut, FormControlOptions options)
    {
        bool missing = cut.Find(HiddenInput).HasAttribute("required") && string.IsNullOrEmpty(options.Value);
        return NativeValidity.Default with
        {
            Valid = !missing,
            ValueMissing = missing,
            ValidationMessage = missing ? "Please select an item in the list." : string.Empty,
        };
    }
}
