using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

// L2 (ADR 06 §1) for the C# half of OtpField's interactions (issue 278, M6 track 7): the module's edit, focus and blur reports
// over a fake twin, turned into Base UI's requests (OTPFieldRoot.tsx:228-266 `setValue`, OTPFieldInput.tsx:101-133
// `commitValue` and the keyboard edits :256-303), the queued focus and completion (:207-226), AutoSubmit's revision, the
// cancel split of ADR 05 Amendments 2026-11-18 item 1 (decision 9 (a)), design points 1–7 (a) (chosen by the user
// 2026-11-19) and the WebAssembly decision. The events themselves come from the module: their [L4] rows run in a browser.
public sealed class OtpFieldInteractionTests : IDisposable
{
    private readonly OtpFieldTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the module attaches to the root with the slots' ids in order, the value, the slot pattern and the optimistic advance")]
    public async Task AttachesWithTheSlots()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.DefaultValue, "12").Add(r => r.Id, "code"));
        await test.PublishSlotsAsync(cut);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(["code", "code-2", "code-3", "code-4", "code-5", "code-6"], options.Slots);
        Assert.Equal(("12", 6, @"\d{1}", true, 2), (options.Value, options.Length, options.SlotPattern, options.Optimistic, options.ActiveIndex));
        Assert.Equal(test.OtpFieldModule.Invocations["attach"][0].Arguments[0], cut.Instance.Element);
    }

    [Fact(DisplayName = "port: a controlled field or one with NormalizeValue is not optimistic")]
    public void ControlledIsNotOptimistic()
    {
        IRenderedComponent<OtpFieldRoot> controlled = test.RenderSix(p => p.Add(r => r.Value, "1"));
        IRenderedComponent<OtpFieldRoot> normalized = test.RenderSix(p => p.Add(r => r.NormalizeValue, (Func<string, string>)(v => v)));

        Assert.Equal(2, test.OtpOptions.Count);
        Assert.All(test.OtpOptions, options => Assert.Null(options.Optimistic));
        Assert.NotNull(controlled.Instance.Element);
        Assert.NotNull(normalized.Instance.Element);
    }

    [Fact(DisplayName = "port: a typed edit commits the normalized value with reason input-change and the update echoes its revision")]
    public async Task TypedEditCommits()
    {
        List<ValueChangeEventArgs<string>> changes = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => changes.Add(e)));

        await test.EditAsync(cut, 0, "input", "7", 1);

        Assert.Equal([("7", EventReason.InputChange)], changes.Select(c => (c.Value, c.Details.Reason)));
        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("7", 1, 1), (options.Value, options.Revision, options.Focus!.Index));
        Assert.True(cut.Find("[role=group]").HasAttribute("data-filled"));
    }

    [Fact(DisplayName = "port: typing several characters into a slot distributes them and queues focus after the last")]
    public async Task TypedTextDistributes()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.DefaultValue, "1"));

        await test.EditAsync(cut, 1, "input", "2a34", 1);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("1234", 4), (options.Value, options.Focus!.Index));
    }

    [Fact(DisplayName = "port: an advanced edit queues no focus")]
    public async Task AdvancedEditQueuesNoFocus()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix();

        await test.EditAsync(cut, 0, "input", "7", 1, advanced: true);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("7", 1), (options.Value, options.Revision));
        Assert.Null(options.Focus);
    }

    [Fact(DisplayName = "port: a paste raises onValueChange with input-paste and queues focus after the pasted characters")]
    public async Task PasteCommits()
    {
        List<EventReason> reasons = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => reasons.Add(e.Details.Reason)));

        await test.EditAsync(cut, 0, "paste", "12 3", 1);

        Assert.Equal([EventReason.InputPaste], reasons);
        Assert.Equal(("123", 3), (test.OtpOptions[^1].Value, test.OtpOptions[^1].Focus!.Index));
    }

    [Theory(DisplayName = "port: the editing keys remove with reason keyboard and focus their target")]
    [InlineData(3, "backspace", "12", 2)]
    [InlineData(1, "backspace", "13", 0)]
    [InlineData(1, "delete", "13", 1)]
    [InlineData(2, "clear", "", 0)]
    public async Task EditingKeys(int index, string kind, string expected, int focus)
    {
        List<EventReason> reasons = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "123")
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => reasons.Add(e.Details.Reason)));

        await test.EditAsync(cut, index, kind, string.Empty, 1);

        Assert.Equal([EventReason.Keyboard], reasons);
        Assert.Equal((expected, focus), (test.OtpOptions[^1].Value, test.OtpOptions[^1].Focus!.Index));
    }

    [Fact(DisplayName = "port: rejected characters raise onValueInvalid with the raw text and the reason")]
    public async Task RejectedCharactersAreInvalid()
    {
        List<OtpFieldInvalidEventArgs> invalid = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.OnValueInvalid, (OtpFieldInvalidEventArgs e) => invalid.Add(e)));

        await test.EditAsync(cut, 0, "paste", "1a2", 1);
        await test.EditAsync(cut, 2, "input", "x", 2);

        Assert.Equal([("1a2", EventReason.InputPaste), ("x", EventReason.InputChange)], invalid.Select(e => (e.Value, e.Details.Reason)));
        Assert.Equal(("12", 2), (test.OtpOptions[^1].Value, test.OtpOptions[^1].Revision));
    }

    [Fact(DisplayName = "port: an emptied slot commits with reason input-clear")]
    public async Task EmptiedSlotClears()
    {
        List<EventReason> reasons = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "123")
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => reasons.Add(e.Details.Reason)));

        await test.EditAsync(cut, 1, "input", string.Empty, 1);

        Assert.Equal([EventReason.InputClear], reasons);
        Assert.Equal("13", test.OtpOptions[^1].Value);
    }

    [Fact(DisplayName = "port: a canceled keyboard edit changes nothing and the update restores the slots with the echoed revision")]
    public async Task CanceledKeyboardEditChangesNothing()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "123")
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => e.Details.Cancel()));

        await test.EditAsync(cut, 2, "backspace", string.Empty, 1);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("123", 1), (options.Value, options.Revision));
        Assert.Null(options.Focus);
    }

    [Fact(DisplayName = "port: a canceled typed edit commits, but no focus moves and no completion is raised (decision 9 (a))")]
    public async Task CanceledTypedEditCommits()
    {
        int completions = 0;
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "12345")
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => e.Details.Cancel())
            .Add(r => r.OnValueComplete, (OtpFieldCompleteEventArgs _) => completions++));

        await test.EditAsync(cut, 5, "input", "6", 1);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("123456", 1), (options.Value, options.Revision));
        Assert.Null(options.Focus);
        Assert.Equal(0, completions);
    }

    [Fact(DisplayName = "port: a canceled advanced edit rolls focus back to its slot (design point 4 (a))")]
    public async Task CanceledAdvancedEditRollsBack()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => e.Details.Cancel()));

        await test.EditAsync(cut, 0, "input", "4", 1, advanced: true);

        OtpFieldOptions options = test.OtpOptions[^1];
        Assert.Equal(("4", 1, true, 0), (options.Value, options.Revision, options.Rollback, options.Focus!.Index));
    }

    [Fact(DisplayName = "port: completing the code raises onValueComplete once, after onValueChange, and AutoSubmit raises the submit revision")]
    public async Task CompletionAndAutoSubmit()
    {
        List<string> calls = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "12345")
            .Add(r => r.AutoSubmit, true)
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => calls.Add($"change {e.Value}"))
            .Add(r => r.OnValueComplete, (OtpFieldCompleteEventArgs e) => calls.Add($"complete {e.Value} {e.Details.Reason}")));
        int submitBefore = test.OtpOptions[^1].SubmitRevision;

        await test.EditAsync(cut, 5, "input", "6", 1);

        Assert.Equal(["change 123456", "complete 123456 InputChange"], calls);
        Assert.Equal(submitBefore + 1, test.OtpOptions[^1].SubmitRevision);
    }

    [Fact(DisplayName = "port: pasting the complete value again raises onValueComplete without a change")]
    public async Task RepastedValueCompletes()
    {
        List<string> calls = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p
            .Add(r => r.DefaultValue, "123456")
            .Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => calls.Add("change"))
            .Add(r => r.OnValueComplete, (OtpFieldCompleteEventArgs e) => calls.Add($"complete {e.Details.Reason}")));

        await test.EditAsync(cut, 0, "paste", "123456", 1);

        Assert.Equal(["complete InputPaste"], calls);
    }

    [Fact(DisplayName = "port: on WebAssembly the edit decision runs the request inside the dispatch")]
    public async Task DecisionRunsTheRequest()
    {
        List<string> changes = [];
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.OnValueChange, (ValueChangeEventArgs<string> e) => changes.Add(e.Value)));

        string verdict = await test.DecideEditAsync(cut, 0, "input", "9", 1);

        Assert.Equal(("run", "9"), (verdict, string.Join(",", changes)));
        Assert.Equal(("9", 1), (test.OtpOptions[^1].Value, test.OtpOptions[^1].Revision));
    }

    [Fact(DisplayName = "port: on WebAssembly a slot's onKeyDown that calls PreventPartHandler stops the edit (OTP-KEY-01, OTP-EVT-16)")]
    public async Task ConsumerKeyDownVetoesTheEdit()
    {
        List<string> keys = [];
        IRenderedComponent<OtpFieldRoot> cut = test.Context.Render<OtpFieldRoot>(p => p
            .Add(r => r.Length, 3)
            .Add(r => r.DefaultValue, "12")
            .AddChildContent(builder =>
            {
                for (int i = 0; i < 3; i++)
                {
                    builder.OpenComponent<OtpFieldInput>(0);
                    builder.SetKey(i);
                    builder.AddAttribute(1, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, e =>
                    {
                        keys.Add(e.Key);
                        e.PreventPartHandler();
                    }));
                    builder.CloseComponent();
                }
            }));

        string verdict = await test.DecideEditAsync(cut, 1, "backspace", string.Empty, 1);

        Assert.Equal(("stop", "Backspace"), (verdict, string.Join(",", keys)));
        Assert.Equal("12", test.OtpOptions[^1].Value);
        Assert.Equal([OtpFieldTwin.KeyPoint], test.OtpOptions[^1].Points!);
        Assert.Null(cut.Find("input").GetAttribute("onkeydown"));
    }

    [Fact(DisplayName = "port: a focus report sets data-focused, and a blur that leaves the root clears it")]
    public async Task FocusAndBlurReports()
    {
        IRenderedComponent<OtpFieldRoot> cut = test.RenderSix(p => p.Add(r => r.DefaultValue, "12"));

        await test.FocusAsync(cut, 1);
        bool focused = cut.Find("[role=group]").HasAttribute("data-focused");
        await test.BlurAsync(cut, 1);

        Assert.True(focused);
        Assert.False(cut.Find("[role=group]").HasAttribute("data-focused"));
    }
}
