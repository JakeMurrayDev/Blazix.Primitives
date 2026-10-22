using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The bUnit context of the Field, Fieldset and Input tests: the library's services, a <see cref="FakeTimeProvider"/>, and
/// bUnit's JS interop mock standing in for the <c>form</c> module (ADR 06 §1, twin contract tests). <see cref="Module"/>
/// is a C# stand-in for the module's side of the conversation: it keeps each attached control's value as the module would
/// (the consumer's value, the default value while unedited), answers a refresh with a <c>sync</c> report, and drives the
/// control's reports (input, blur, keydown) into its twin with the native state a test chooses.
/// </summary>
public sealed class FieldTestContext : IDisposable
{
    public const string FormModulePath = "./_content/Blazix.Primitives/js/form.js";

    public FieldTestContext()
    {
        Context.Services.AddBlazixPrimitives();
        Context.Services.AddSingleton<TimeProvider>(Clock);
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        FormModule = Context.JSInterop.SetupModule(FormModulePath);
        FormHandle = FormModule.SetupModule("attach", _ => true);
        FormHandle.SetupVoid("update", _ => true).SetVoidResult();
        FormHandle.SetupVoid("dispose", _ => true).SetVoidResult();
        Module = new FormModuleStandIn(this);
    }

    public BunitContext Context { get; } = new();

    public FakeTimeProvider Clock { get; } = new();

    public BunitJSModuleInterop FormModule { get; }

    public BunitJSModuleInterop FormHandle { get; }

    internal FormModuleStandIn Module { get; }

    /// <inheritdoc />
    public void Dispose() => Context.Dispose();

    /// <summary>The options JSON of every attach of the form module, in order.</summary>
    public IReadOnlyList<string> AttachOptions() => [.. FormModule.Invocations["attach"].Select(invocation => (string)invocation.Arguments[1]!)];

    /// <summary>The options JSON of every update sent through the form module's handles, in order.</summary>
    public IReadOnlyList<string> UpdateOptions() => [.. FormHandle.Invocations["update"].Select(invocation => (string)invocation.Arguments[0]!)];

    internal static FormOptions Parse(string json) => JsonSerializer.Deserialize(json, BlazixJsonContext.Default.FormOptions)!;
}

/// <summary>The module's side of the conversation, in C#: one entry per attached control or label, in attach order.</summary>
internal sealed class FormModuleStandIn(FieldTestContext host)
{
    private readonly List<Attached> answered = [];

    /// <summary>The attached controls, in attach order.</summary>
    public IReadOnlyList<Attached> Controls => [.. Attachments().Where(a => a.IsControl && a.Twin is not null)];

    /// <summary>The attached labels, in attach order.</summary>
    public IReadOnlyList<Attached> Labels => [.. Attachments().Where(a => !a.IsControl && a.Twin is not null)];

    /// <summary>The control attached last.</summary>
    public Attached Control => Controls[^1];

    /// <summary>Answers every refresh the controls asked for, as the module does on update.</summary>
    public void Settle<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        foreach (Attached control in Controls)
        {
            control.Settle(cut);
        }
    }

    private IEnumerable<Attached> Attachments()
    {
        IReadOnlyList<JSRuntimeInvocation> attaches = host.FormModule.Invocations["attach"];
        for (int i = answered.Count; i < attaches.Count; i++)
        {
            JSRuntimeInvocation invocation = attaches[i];
            FormTwin? twin;
            try
            {
                twin = ((DotNetObjectReference<FormTwin>)invocation.Arguments[2]!).Value;
            }
            catch (ObjectDisposedException)
            {
                // The part that attached it has been disposed since.
                twin = null;
            }

            answered.Add(new Attached(host, twin, FieldTestContext.Parse((string)invocation.Arguments[1]!)));
        }

        return answered;
    }

    /// <summary>One attached element and the state the module keeps for it.</summary>
    internal sealed class Attached
    {
        private readonly FieldTestContext host;
        private int answeredRevision;
        private bool edited;
        private FormControlOptions? options;

        public Attached(FieldTestContext host, FormTwin? twin, FormOptions attachOptions)
        {
            this.host = host;
            Twin = twin;
            IsControl = attachOptions.Control is not null;
            options = attachOptions.Control;
            if (options is not null)
            {
                DomValue = options.Controlled ? options.Value ?? string.Empty : options.DefaultValue ?? string.Empty;
                answeredRevision = options.Revision;
            }
        }

        public FormTwin? Twin { get; }

        public bool IsControl { get; }

        /// <summary>The element's value as the module keeps it.</summary>
        public string DomValue { get; set; } = string.Empty;

        /// <summary>The native state the module reports; tests set it before an event.</summary>
        public NativeValidity Native { get; set; } = NativeValidity.Default;

        /// <summary>The control options the module last received.</summary>
        public FormControlOptions? Options => options;

        /// <summary>Types a value: the module's input report.</summary>
        public void Input<TComponent>(IRenderedComponent<TComponent> cut, string value, NativeValidity? native = null, bool defaultPrevented = false)
            where TComponent : IComponent
        {
            DomValue = value;
            edited = true;
            Native = native ?? Native;
            Report(cut, "input", new EventSnapshot { Kind = EventKind.InputEvent, Type = "input", DefaultPrevented = defaultPrevented, Target = new EventTargetInfo { TagName = "INPUT" } });
            Settle(cut);
        }

        /// <summary>The module's blur report.</summary>
        public void Blur<TComponent>(IRenderedComponent<TComponent> cut, NativeValidity? native = null)
            where TComponent : IComponent
        {
            Native = native ?? Native;
            Report(cut, "blur", new EventSnapshot { Kind = EventKind.FocusEvent, Type = "blur", Target = new EventTargetInfo { TagName = "INPUT" } });
            Settle(cut);
        }

        /// <summary>The module's keydown report, and for Enter in an input of the enclosing form, its deferred enter report.</summary>
        public void KeyDown<TComponent>(IRenderedComponent<TComponent> cut, string key, bool sameForm = false, bool defaultPrevented = false, string tagName = "INPUT")
            where TComponent : IComponent
        {
            EventSnapshot snapshot = new() { Kind = EventKind.KeyboardEvent, Type = "keydown", Key = key, Code = key, DefaultPrevented = defaultPrevented, Target = new EventTargetInfo { TagName = tagName } };
            Report(cut, "keydown", snapshot, sameForm);
            Settle(cut);
        }

        /// <summary>The module's deferred enter report.</summary>
        public void Enter<TComponent>(IRenderedComponent<TComponent> cut, bool sameForm = true)
            where TComponent : IComponent
        {
            Report(cut, "enter", null, sameForm);
            Settle(cut);
        }

        /// <summary>The module's report that the element already had focus when it was attached or enabled.</summary>
        public void Focused<TComponent>(IRenderedComponent<TComponent> cut)
            where TComponent : IComponent
        {
            Report(cut, "focus", null);
        }

        /// <summary>Applies the options the control sent and answers a refresh.</summary>
        public void Settle<TComponent>(IRenderedComponent<TComponent> cut)
            where TComponent : IComponent
        {
            if (!IsControl)
            {
                return;
            }

            IReadOnlyList<string> updates = host.UpdateOptions();
            FormControlOptions? latest = null;
            foreach (string json in updates)
            {
                FormControlOptions? control = FieldTestContext.Parse(json).Control;
                if (control is not null)
                {
                    latest = control;
                }
            }

            if (latest is null || ReferenceEquals(latest, options))
            {
                return;
            }

            FormControlOptions? previous = options;
            options = latest;
            if (latest.Controlled && latest.Value is not null)
            {
                DomValue = latest.Value;
            }
            else if (!latest.Controlled && previous?.DefaultValue != latest.DefaultValue && !edited)
            {
                DomValue = latest.DefaultValue ?? string.Empty;
            }

            if (latest.Revision > answeredRevision)
            {
                answeredRevision = latest.Revision;
                Report(cut, "sync", null);
            }
        }

        private void Report<TComponent>(IRenderedComponent<TComponent> cut, string type, EventSnapshot? snapshot, bool sameForm = false)
            where TComponent : IComponent
        {
            FormControlReport report = new()
            {
                Type = type,
                Value = DomValue,
                Validity = Native with { OwnMessageInstalled = options?.CustomValidity is not null },
                Event = snapshot,
                SameForm = sameForm,
                Revision = answeredRevision,
            };
            string json = JsonSerializer.Serialize(report, BlazixJsonContext.Default.FormControlReport);
            cut.InvokeAsync(() => Twin!.OnControlReport(json));
        }
    }
}
