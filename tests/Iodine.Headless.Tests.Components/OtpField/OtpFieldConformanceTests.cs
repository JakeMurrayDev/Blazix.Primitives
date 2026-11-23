using Iodine.Headless.Rendering;
using Iodine.Headless.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

// OTPFieldRoot.test.tsx: describeConformance(<OTPField.Root length={1} />, () => ({ refInstanceof: window.HTMLDivElement })).
// OTP-API-19, OTP-API-20 (issue 278, M6 track 7).
/// <summary>The conformance suite on <see cref="OtpFieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class OtpFieldRootConformanceTests : PartConformanceTests<OtpFieldRoot>
{
    public OtpFieldRootConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    protected override ConformanceOptions<OtpFieldRoot> Options { get; } = new("div") { Required = p => p.Add(x => x.Length, 1) };

    /// <inheritdoc />
    protected override ElementReference? ElementOf(OtpFieldRoot part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<OtpFieldRoot> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<OtpFieldRootState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<OtpFieldRoot> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<OtpFieldRootState, string?>)(_ => classValue()));
    }
}

// OTPFieldInput.test.tsx: describeConformance(<OTPField.Input />, () => ({ refInstanceof: window.HTMLInputElement, render:
// inside <OTPField.Root length={1}> })). OTP-API-23.
/// <summary>The conformance suite on <see cref="OtpFieldInput"/> inside an <see cref="OtpFieldRoot"/>: the shared RND-DOM-36..49 tests with Base UI's titles.</summary>
public sealed class OtpFieldInputConformanceTests : PartConformanceTests<OtpFieldInput>
{
    public OtpFieldInputConformanceTests()
    {
        // The twins' imports and attaches are not the subject here.
        Context.JSInterop.Mode = JSRuntimeMode.Loose;
        Context.RenderTree.Add<OtpFieldRoot>(p => p.Add(x => x.Length, 1));
    }

    /// <inheritdoc />
    protected override ConformanceOptions<OtpFieldInput> Options { get; } = new("input");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(OtpFieldInput part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<OtpFieldInput> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<OtpFieldInputState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<OtpFieldInput> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<OtpFieldInputState, string?>)(_ => classValue()));
    }
}
