using Blazix.Primitives.Fixtures.Client.AccordionFixtures;
using Blazix.Primitives.Fixtures.Client.Allocation;
using Blazix.Primitives.Fixtures.Client.AvatarFixtures;
using Blazix.Primitives.Fixtures.Client.ButtonBehavior;
using Blazix.Primitives.Fixtures.Client.ButtonFixtures;
using Blazix.Primitives.Fixtures.Client.CheckboxFixtures;
using Blazix.Primitives.Fixtures.Client.CheckboxGroupFixtures;
using Blazix.Primitives.Fixtures.Client.CollapsibleFixtures;
using Blazix.Primitives.Fixtures.Client.CompositeFixtures;
using Blazix.Primitives.Fixtures.Client.FieldFixtures;
using Blazix.Primitives.Fixtures.Client.FieldsetFixtures;
using Blazix.Primitives.Fixtures.Client.FloatingFixtures;
using Blazix.Primitives.Fixtures.Client.FormFixtures;
using Blazix.Primitives.Fixtures.Client.InputFixtures;
using Blazix.Primitives.Fixtures.Client.Interactions;
using Blazix.Primitives.Fixtures.Client.KeepAlive;
using Blazix.Primitives.Fixtures.Client.MeterFixtures;
using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Blazix.Primitives.Fixtures.Client.PortalFixtures;
using Blazix.Primitives.Fixtures.Client.Prerender;
using Blazix.Primitives.Fixtures.Client.Probe;
using Blazix.Primitives.Fixtures.Client.ProgressFixtures;
using Blazix.Primitives.Fixtures.Client.RadioFixtures;
using Blazix.Primitives.Fixtures.Client.RadioGroupFixtures;
using Blazix.Primitives.Fixtures.Client.ScrollLockFixtures;
using Blazix.Primitives.Fixtures.Client.Separator;
using Blazix.Primitives.Fixtures.Client.SwitchFixtures;
using Blazix.Primitives.Fixtures.Client.TabsFixtures;
using Blazix.Primitives.Fixtures.Client.ToggleFixtures;
using Blazix.Primitives.Fixtures.Client.ToggleGroupFixtures;
using Blazix.Primitives.Fixtures.Client.ToolbarFixtures;
using Blazix.Primitives.Fixtures.Client.TooltipFixtures;

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
        ["interactions-transport"] = new(typeof(InteractionsTransportFixture)),
        ["interactions-press"] = new(typeof(InteractionsPressFixture)),
        ["interactions-press-late-session"] = new(typeof(InteractionsPressLateSessionFixture)),
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
        ["accordion-basic"] = new(typeof(AccordionBasicFixture)),
        ["accordion-css-transition"] = new(typeof(AccordionCssTransitionFixture)),
        ["accordion-initially-open"] = new(typeof(AccordionInitiallyOpenFixture), Prerender: true),
        ["accordion-reattach"] = new(typeof(AccordionReattachFixture)),
        ["accordion-hidden-until-found"] = new(typeof(AccordionHiddenUntilFoundFixture)),
        ["meter-basic"] = new(typeof(MeterBasicFixture)),
        ["meter-prerender"] = new(typeof(MeterPrerenderFixture), Prerender: true),
        ["progress-basic"] = new(typeof(ProgressBasicFixture)),
        ["progress-prerender"] = new(typeof(ProgressPrerenderFixture), Prerender: true),
        ["composite-list"] = new(typeof(CompositeListFixture)),
        ["composite-rtl"] = new(typeof(CompositeRtlFixture)),
        ["composite-reorder"] = new(typeof(CompositeReorderFixture)),
        ["composite-selection"] = new(typeof(CompositeSelectionFixture)),
        ["composite-nested"] = new(typeof(CompositeNestedFixture)),
        ["composite-flags"] = new(typeof(CompositeFlagsFixture)),
        ["composite-follow"] = new(typeof(CompositeFollowFixture)),
        ["composite-registration"] = new(typeof(CompositeRegistrationFixture)),
        ["field-basic"] = new(typeof(FieldBasicFixture)),
        ["field-validation"] = new(typeof(FieldValidationFixture)),
        ["field-label-non-native"] = new(typeof(FieldLabelNonNativeFixture)),
        ["field-autofocus"] = new(typeof(FieldAutofocusFixture), Prerender: true),
        ["field-edit-form"] = new(typeof(FieldEditFormFixture)),
        ["field-error"] = new(typeof(FieldErrorFixture)),
        ["field-custom-validity"] = new(typeof(FieldCustomValidityFixture)),
        ["field-form"] = new(typeof(FieldFormFixture)),
        ["fieldset-basic"] = new(typeof(FieldsetBasicFixture)),
        ["fieldset-prerender"] = new(typeof(FieldsetPrerenderFixture), Prerender: true),
        ["input-showcase"] = new(typeof(InputShowcaseFixture)),
        ["form-submit"] = new(typeof(FormSubmitFixture)),
        ["form-blocked"] = new(typeof(FormBlockedFixture)),
        ["form-native-validation"] = new(typeof(FormNativeValidationFixture)),
        ["form-external-errors"] = new(typeof(FormExternalErrorsFixture)),
        ["form-errors"] = new(typeof(FormErrorsFixture)),
        ["form-registration"] = new(typeof(FormRegistrationFixture)),
        ["switch-basic"] = new(typeof(SwitchBasicFixture)),
        ["switch-labels"] = new(typeof(SwitchLabelsFixture)),
        ["switch-form"] = new(typeof(SwitchFormFixture)),
        ["switch-field"] = new(typeof(SwitchFieldFixture)),
        ["switch-form-field"] = new(typeof(SwitchFormFieldFixture)),
        ["switch-edit-form"] = new(typeof(SwitchEditFormFixture)),
        ["checkbox-basic"] = new(typeof(CheckboxBasicFixture)),
        ["checkbox-labels"] = new(typeof(CheckboxLabelsFixture)),
        ["checkbox-form"] = new(typeof(CheckboxFormFixture)),
        ["checkbox-indicator"] = new(typeof(CheckboxIndicatorFixture)),
        ["checkbox-field"] = new(typeof(CheckboxFieldFixture)),
        ["checkbox-enter"] = new(typeof(CheckboxEnterFixture)),
        ["checkbox-names"] = new(typeof(CheckboxNamesFixture)),
        ["checkbox-prerender"] = new(typeof(CheckboxPrerenderFixture), Prerender: true),
        ["checkbox-base-ui-form"] = new(typeof(CheckboxBaseUiFormFixture)),
        ["checkbox-group-field"] = new(typeof(CheckboxGroupFieldFixture)),
        ["checkbox-group-prerender"] = new(typeof(CheckboxGroupPrerenderFixture), Prerender: true),
        ["checkbox-group-form"] = new(typeof(CheckboxGroupFormFixture)),
        ["checkbox-group-portal"] = new(typeof(CheckboxGroupPortalFixture)),
        ["checkbox-group-errors"] = new(typeof(CheckboxGroupErrorsFixture)),
        ["radio-standalone"] = new(typeof(RadioStandaloneFixture)),
        ["radio-native-button"] = new(typeof(RadioNativeButtonFixture)),
        ["radio-indicator"] = new(typeof(RadioIndicatorFixture)),
        ["radio-group-basic"] = new(typeof(RadioGroupBasicFixture)),
        ["radio-group-controlled"] = new(typeof(RadioGroupControlledFixture)),
        ["radio-group-rtl"] = new(typeof(RadioGroupRtlFixture)),
        ["radio-group-field"] = new(typeof(RadioGroupFieldFixture)),
        ["radio-group-fieldset"] = new(typeof(RadioGroupFieldsetFixture)),
        ["radio-group-form"] = new(typeof(RadioGroupFormFixture)),
        ["radio-group-portal"] = new(typeof(RadioGroupPortalFixture)),
        ["radio-group-edit-form"] = new(typeof(RadioGroupEditFormFixture)),
        ["radio-group-allocation"] = new(typeof(RadioGroupAllocationFixture)),
        ["toggle-standalone"] = new(typeof(ToggleStandaloneFixture)),
        ["toggle-composite-root"] = new(typeof(ToggleCompositeRootFixture)),
        ["toggle-group-orientation"] = new(typeof(ToggleGroupOrientationFixture)),
        ["toggle-group-basic"] = new(typeof(ToggleGroupBasicFixture)),
        ["tabs-keyboard"] = new(typeof(TabsKeyboardFixture)),
        ["tabs-list"] = new(typeof(TabsListFixture)),
        ["tabs-activation"] = new(typeof(TabsActivationFixture)),
        ["tabs-direction"] = new(typeof(TabsDirectionFixture)),
        ["tabs-panels"] = new(typeof(TabsPanelsFixture)),
        ["tabs-indicator"] = new(typeof(TabsIndicatorFixture)),
        ["tabs-focus"] = new(typeof(TabsFocusFixture)),
        ["toolbar-orientation"] = new(typeof(ToolbarOrientationFixture)),
        ["toolbar-keys"] = new(typeof(ToolbarKeysFixture)),
        ["toolbar-input"] = new(typeof(ToolbarInputFixture)),
        ["toolbar-button"] = new(typeof(ToolbarButtonFixture)),
        ["toolbar-switch"] = new(typeof(ToolbarSwitchFixture)),
        ["toolbar-disabled"] = new(typeof(ToolbarDisabledFixture)),
        ["toolbar-toggle"] = new(typeof(ToolbarToggleFixture)),
        ["popup-conformance"] = new(typeof(PopupConformanceFixture)),
        ["popup-triggers"] = new(typeof(PopupTriggersFixture)),
        ["popup-unmount"] = new(typeof(PopupUnmountFixture)),
        ["portal-basic"] = new(typeof(PortalBasicFixture)),
        ["portal-prerender"] = new(typeof(PortalPrerenderFixture), Prerender: true),
        ["scroll-lock-basic"] = new(typeof(ScrollLockBasicFixture)),
        ["positioning-suite"] = new(typeof(PositioningSuiteFixture)),
        ["positioning-origin"] = new(typeof(PositioningOriginFixture)),
        ["positioning-lifecycle"] = new(typeof(PositioningLifecycleFixture)),
        ["positioning-prerender"] = new(typeof(PositioningPrerenderFixture), Prerender: true),
        ["tooltip-conformance"] = new(typeof(TooltipConformanceFixture)),
        ["tooltip-triggers"] = new(typeof(TooltipTriggersFixture)),
        ["tooltip-root"] = new(typeof(TooltipRootFixture)),
        ["tooltip-provider"] = new(typeof(TooltipProviderFixture)),
        ["tooltip-nested"] = new(typeof(TooltipNestedFixture)),
        ["tooltip-layout"] = new(typeof(TooltipLayoutFixture)),
        ["tooltip-handoff"] = new(typeof(TooltipHandoffFixture)),
        ["tooltip-positioner"] = new(typeof(TooltipPositionerFixture)),
        ["tooltip-toolbar"] = new(typeof(TooltipToolbarFixture)),
        ["tooltip-prerender"] = new(typeof(TooltipPrerenderFixture), Prerender: true),
    };
}
