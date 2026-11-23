using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;

namespace Iodine.Headless;

// OTP-STR-05, OTP-API-25, OTP-DOM-27 (Base UI's OTPField.Separator is Separator; otp-field/index.parts.ts:3,
// separator/Separator.tsx:16-24). ADR 08 §3: the state record is sealed, partial and [PartState]; the default projection gives
// data-orientation.
/// <summary>Represents the state of a one-time code field separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record OtpFieldSeparatorState(Orientation Orientation);

// otp-field/index.ts's OTPFieldSeparatorDataAttributes (Separator's).
/// <summary>Provides the names of the data attributes a one-time code field separator renders.</summary>
public static class OtpFieldSeparatorDataAttributes
{
    /// <summary>Represents the attribute that names the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's OTPField.Separator (otp-field/index.parts.ts:3 re-exports Separator); the contract is docs/components/otp-field.md
// §3.3 (OTP-STR-05, OTP-API-25, OTP-DOM-27). Its own part over SeparatorAttributes (the ToolbarSeparator and
// ContextMenuSeparator precedent): it reads no state of the field and does not register as a slot.
/// <summary>
/// Represents a line that separates groups of slots in a one-time code field, Base UI's <c>OTPField.Separator</c>, rendered
/// as a <c>div</c>.
/// </summary>
public sealed class OtpFieldSeparator : PartBase<OtpFieldSeparatorState>
{
    private OtpFieldSeparatorState? state;

    /// <summary>Gets or sets the separator's orientation.</summary>
    /// <value><see cref="Iodine.Headless.Orientation.Horizontal"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override OtpFieldSeparatorState State
    {
        get
        {
            if (state is null || state.Orientation != Orientation)
            {
                state = new OtpFieldSeparatorState(Orientation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => SeparatorAttributes.Write(attributes, Orientation);
}
