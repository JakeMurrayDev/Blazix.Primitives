using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless.Tests;

// OTPFieldRoot.spec.tsx and OTPFieldInput.spec.tsx, the type-only specs (issue 278, M6 track 7): what the C# types declare.
// `// @ts-expect-error - length is required` becomes [EditorRequired] on Length (RZ2012 is an analyzer warning, so a
// reflection title is the port's check); `@ts-expect-error - slot order is inferred from render order` becomes the absence of
// an Index parameter; `sanitizeValue` was renamed, so no such parameter exists either.
/// <summary>The parameters the one-time code field's parts declare.</summary>
public sealed class OtpFieldParameterTests
{
    [Fact(DisplayName = "port: OtpFieldRoot's Length is a required int parameter (OTP-API-01)")]
    [Trait("Requirement", "OTP-API-01")]
    public void Length_is_a_required_int_parameter()
    {
        PropertyInfo length = typeof(OtpFieldRoot).GetProperty(nameof(OtpFieldRoot.Length))!;

        Assert.Equal(typeof(int), length.PropertyType);
        Assert.NotNull(length.GetCustomAttribute<ParameterAttribute>());
        Assert.NotNull(length.GetCustomAttribute<EditorRequiredAttribute>());
    }

    [Fact(DisplayName = "port: OtpFieldInput declares no Index parameter, its order coming from render order (OTP-API-24)")]
    [Trait("Requirement", "OTP-API-24")]
    [Trait("Requirement", "OTP-STR-03")]
    public void OtpFieldInput_declares_no_index_parameter()
    {
        Assert.Null(typeof(OtpFieldInput).GetProperty("Index", BindingFlags.Public | BindingFlags.Instance));
        Assert.DoesNotContain(
            typeof(OtpFieldInput).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            property => property.Name == "Index" && property.GetCustomAttribute<ParameterAttribute>() is not null);
    }

    [Fact(DisplayName = "port: OtpFieldRoot takes NormalizeValue and no SanitizeValue parameter (OTP-API-08)")]
    [Trait("Requirement", "OTP-API-08")]
    public void Normalize_value_and_no_sanitize_value()
    {
        PropertyInfo normalize = typeof(OtpFieldRoot).GetProperty(nameof(OtpFieldRoot.NormalizeValue))!;

        Assert.Equal(typeof(Func<string, string>), normalize.PropertyType);
        Assert.NotNull(normalize.GetCustomAttribute<ParameterAttribute>());
        Assert.Null(typeof(OtpFieldRoot).GetProperty("SanitizeValue"));
    }

    [Fact(DisplayName = "port: the callbacks carry change details for a change and generic details for invalid text and completion (OTP-EVT-06, OTP-API-04..06)")]
    [Trait("Requirement", "OTP-EVT-06")]
    [Trait("Requirement", "OTP-API-04")]
    [Trait("Requirement", "OTP-API-05")]
    [Trait("Requirement", "OTP-API-06")]
    public void The_callbacks_carry_their_details()
    {
        Assert.Equal(typeof(EventCallback<ValueChangeEventArgs<string>>), typeof(OtpFieldRoot).GetProperty(nameof(OtpFieldRoot.OnValueChange))!.PropertyType);
        Assert.Equal(typeof(EventCallback<OtpFieldInvalidEventArgs>), typeof(OtpFieldRoot).GetProperty(nameof(OtpFieldRoot.OnValueInvalid))!.PropertyType);
        Assert.Equal(typeof(EventCallback<OtpFieldCompleteEventArgs>), typeof(OtpFieldRoot).GetProperty(nameof(OtpFieldRoot.OnValueComplete))!.PropertyType);
        Assert.Equal(typeof(GenericEventDetails), typeof(OtpFieldInvalidEventArgs).GetProperty(nameof(OtpFieldInvalidEventArgs.Details))!.PropertyType);
        Assert.Equal(typeof(GenericEventDetails), typeof(OtpFieldCompleteEventArgs).GetProperty(nameof(OtpFieldCompleteEventArgs.Details))!.PropertyType);
        Assert.Equal(typeof(string), typeof(OtpFieldCompleteEventArgs).GetProperty(nameof(OtpFieldCompleteEventArgs.Value))!.PropertyType);
    }
}
