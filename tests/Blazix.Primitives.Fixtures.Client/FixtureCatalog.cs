using Blazix.Primitives.Fixtures.Client.Allocation;
using Blazix.Primitives.Fixtures.Client.AvatarFixtures;
using Blazix.Primitives.Fixtures.Client.ButtonBehavior;
using Blazix.Primitives.Fixtures.Client.ButtonFixtures;
using Blazix.Primitives.Fixtures.Client.CollapsibleFixtures;
using Blazix.Primitives.Fixtures.Client.FieldFixtures;
using Blazix.Primitives.Fixtures.Client.FieldsetFixtures;
using Blazix.Primitives.Fixtures.Client.FormFixtures;
using Blazix.Primitives.Fixtures.Client.InputFixtures;
using Blazix.Primitives.Fixtures.Client.KeepAlive;
using Blazix.Primitives.Fixtures.Client.MeterFixtures;
using Blazix.Primitives.Fixtures.Client.Prerender;
using Blazix.Primitives.Fixtures.Client.Probe;
using Blazix.Primitives.Fixtures.Client.ProgressFixtures;
using Blazix.Primitives.Fixtures.Client.Separator;

namespace Blazix.Primitives.Fixtures.Client;

/// <summary>
/// Every fixture the host serves under <c>/{mode}/{fixture}</c> (ADR 09 §1). Prerendering is off except where a row
/// says otherwise (ADR 06 §2); a fixture with several roots renders them in document order with <c>RootIndex</c>.
/// </summary>
public static class FixtureCatalog
{
    public static IReadOnlyDictionary<string, FixtureEntry> Entries { get; } = new Dictionary<string, FixtureEntry>(StringComparer.Ordinal)
    {
        ["probe"] = new(typeof(ProbeFixture)),
        ["prerender"] = new(typeof(PrerenderFixture), Prerender: true, Roots: 2),
        ["allocation"] = new(typeof(AllocationFixture)),
        ["keep-alive"] = new(typeof(KeepAliveFixture)),
        ["button-behavior"] = new(typeof(ButtonBehaviorFixture)),
        ["button-native"] = new(typeof(ButtonNativeFixture)),
        ["button-focusable"] = new(typeof(ButtonFocusableFixture)),
        ["button-custom"] = new(typeof(ButtonCustomFixture)),
        ["button-link"] = new(typeof(ButtonLinkFixture)),
        ["button-loading"] = new(typeof(ButtonLoadingFixture)),
        ["separator-showcase"] = new(typeof(SeparatorFixture)),
        ["avatar-preload"] = new(typeof(AvatarPreloadFixture)),
        ["avatar-kept-mounted"] = new(typeof(AvatarKeptMountedFixture)),
        ["avatar-delay"] = new(typeof(AvatarDelayFixture)),
        ["avatar-render-function"] = new(typeof(AvatarRenderFunctionFixture)),
        ["avatar-prerender-preload"] = new(typeof(AvatarPrerenderPreloadFixture), Prerender: true),
        ["avatar-prerender-kept-mounted"] = new(typeof(AvatarPrerenderKeptMountedFixture), Prerender: true),
        ["collapsible-basic"] = new(typeof(CollapsibleBasicFixture)),
        ["collapsible-css-transition"] = new(typeof(CollapsibleCssTransitionFixture)),
        ["collapsible-css-animation"] = new(typeof(CollapsibleCssAnimationFixture)),
        ["collapsible-render-function"] = new(typeof(CollapsibleRenderFunctionFixture)),
        ["collapsible-initially-open"] = new(typeof(CollapsibleInitiallyOpenFixture), Prerender: true),
        ["collapsible-reattach"] = new(typeof(CollapsibleReattachFixture)),
        ["collapsible-hidden-until-found"] = new(typeof(CollapsibleHiddenUntilFoundFixture)),
        ["meter-basic"] = new(typeof(MeterBasicFixture)),
        ["meter-prerender"] = new(typeof(MeterPrerenderFixture), Prerender: true),
        ["progress-basic"] = new(typeof(ProgressBasicFixture)),
        ["progress-prerender"] = new(typeof(ProgressPrerenderFixture), Prerender: true),
        ["field-basic"] = new(typeof(FieldBasicFixture)),
        ["field-validation"] = new(typeof(FieldValidationFixture)),
        ["field-label-non-native"] = new(typeof(FieldLabelNonNativeFixture)),
        ["field-autofocus"] = new(typeof(FieldAutofocusFixture), Prerender: true),
        ["field-edit-form"] = new(typeof(FieldEditFormFixture)),
        ["fieldset-basic"] = new(typeof(FieldsetBasicFixture)),
        ["fieldset-prerender"] = new(typeof(FieldsetPrerenderFixture), Prerender: true),
        ["input-showcase"] = new(typeof(InputShowcaseFixture)),
        ["form-submit"] = new(typeof(FormSubmitFixture)),
        ["form-blocked"] = new(typeof(FormBlockedFixture)),
        ["form-native-validation"] = new(typeof(FormNativeValidationFixture)),
        ["form-external-errors"] = new(typeof(FormExternalErrorsFixture)),
    };
}
