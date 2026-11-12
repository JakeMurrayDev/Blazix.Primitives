using Blazix.Primitives.Fixtures.Client.AccordionFixtures;
using Blazix.Primitives.Fixtures.Client.AlertDialogFixtures;
using Blazix.Primitives.Fixtures.Client.Allocation;
using Blazix.Primitives.Fixtures.Client.AvatarFixtures;
using Blazix.Primitives.Fixtures.Client.ButtonBehavior;
using Blazix.Primitives.Fixtures.Client.ButtonFixtures;
using Blazix.Primitives.Fixtures.Client.CheckboxFixtures;
using Blazix.Primitives.Fixtures.Client.CheckboxGroupFixtures;
using Blazix.Primitives.Fixtures.Client.ContextMenuFixtures;
using Blazix.Primitives.Fixtures.Client.CollapsibleFixtures;
using Blazix.Primitives.Fixtures.Client.ComboboxFixtures;
using Blazix.Primitives.Fixtures.Client.CompositeFixtures;
using Blazix.Primitives.Fixtures.Client.DialogFixtures;
using Blazix.Primitives.Fixtures.Client.FieldFixtures;
using Blazix.Primitives.Fixtures.Client.FieldsetFixtures;
using Blazix.Primitives.Fixtures.Client.FloatingFixtures;
using Blazix.Primitives.Fixtures.Client.FocusManagerFixtures;
using Blazix.Primitives.Fixtures.Client.FormFixtures;
using Blazix.Primitives.Fixtures.Client.InputFixtures;
using Blazix.Primitives.Fixtures.Client.ListValuesFixtures;
using Blazix.Primitives.Fixtures.Client.Interactions;
using Blazix.Primitives.Fixtures.Client.KeepAlive;
using Blazix.Primitives.Fixtures.Client.MeterFixtures;
using Blazix.Primitives.Fixtures.Client.MenuFixtures;
using Blazix.Primitives.Fixtures.Client.MenubarFixtures;
using Blazix.Primitives.Fixtures.Client.NavigationMenuFixtures;
using Blazix.Primitives.Fixtures.Client.PopoverFixtures;
using Blazix.Primitives.Fixtures.Client.SelectFixtures;
using Blazix.Primitives.Fixtures.Client.PopupFixtures;
using Blazix.Primitives.Fixtures.Client.PortalFixtures;
using Blazix.Primitives.Fixtures.Client.PreviewCardFixtures;
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
        ["interactions-mousedown"] = new(typeof(InteractionsMouseDownFixture)),
        ["interactions-platform-flags"] = new(typeof(InteractionsPlatformFlagsFixture)),
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
        ["focus-manager-modal"] = new(typeof(FocusManagerModalFixture)),
        ["focus-manager-kept-mounted"] = new(typeof(FocusManagerKeptMountedFixture)),
        ["focus-manager-empty"] = new(typeof(FocusManagerEmptyFixture)),
        ["focus-manager-restore"] = new(typeof(FocusManagerRestoreFixture)),
        ["focus-manager-non-modal"] = new(typeof(FocusManagerNonModalFixture)),
        ["focus-manager-composite"] = new(typeof(FocusManagerCompositeFixture)),
        ["focus-manager-portal"] = new(typeof(FocusManagerPortalFixture)),
        ["focus-manager-combobox"] = new(typeof(FocusManagerComboboxFixture)),
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
        ["tabs-dialog"] = new(typeof(TabsDialogFixture)),
        ["tabs-popover"] = new(typeof(TabsPopoverFixture)),
        ["toolbar-orientation"] = new(typeof(ToolbarOrientationFixture)),
        ["toolbar-keys"] = new(typeof(ToolbarKeysFixture)),
        ["toolbar-input"] = new(typeof(ToolbarInputFixture)),
        ["toolbar-button"] = new(typeof(ToolbarButtonFixture)),
        ["toolbar-switch"] = new(typeof(ToolbarSwitchFixture)),
        ["toolbar-disabled"] = new(typeof(ToolbarDisabledFixture)),
        ["toolbar-toggle"] = new(typeof(ToolbarToggleFixture)),
        ["toolbar-dialog"] = new(typeof(ToolbarDialogFixture)),
        ["toolbar-popover"] = new(typeof(ToolbarPopoverFixture)),
        ["popup-conformance"] = new(typeof(PopupConformanceFixture)),
        ["popup-triggers"] = new(typeof(PopupTriggersFixture)),
        ["popup-unmount"] = new(typeof(PopupUnmountFixture)),
        ["popup-lifecycle"] = new(typeof(PopupLifecycleFixture)),
        ["popover-conformance"] = new(typeof(PopoverConformanceFixture)),
        ["popover-focus"] = new(typeof(PopoverFocusFixture)),
        ["popover-detached"] = new(typeof(PopoverDetachedFixture)),
        ["popover-hover"] = new(typeof(PopoverHoverFixture)),
        ["popover-nested"] = new(typeof(PopoverNestedFixture)),
        ["popover-dialog-nested"] = new(typeof(PopoverDialogNestedFixture)),
        ["popover-root"] = new(typeof(PopoverRootFixture)),
        ["popover-positioner"] = new(typeof(PopoverPositionerFixture)),
        ["popover-triggers"] = new(typeof(PopoverTriggersFixture)),
        ["popover-tooltip-close"] = new(typeof(PopoverTooltipCloseFixture)),
        ["popover-viewport"] = new(typeof(PopoverViewportFixture)),

        // M5 track 6 (issue 172): Menu's fixtures.
        ["menu-conformance"] = new(typeof(MenuConformanceFixture)),
        ["menu-triggers"] = new(typeof(MenuTriggersFixture)),
        ["menu-viewport"] = new(typeof(MenuViewportFixture)),
        ["menu-focus"] = new(typeof(MenuFocusFixture)),
        ["menu-positioner"] = new(typeof(MenuPositionerFixture)),
        ["menu-list"] = new(typeof(MenuListFixture)),
        ["menu-nested"] = new(typeof(MenuNestedFixture)),
        ["menu-measure"] = new(typeof(MenuMeasureFixture)),
        ["menu-root"] = new(typeof(MenuRootFixture)),
        ["context-menu-root"] = new(typeof(ContextMenuRootFixture)),
        ["menubar-root"] = new(typeof(MenubarRootFixture)),
        ["menubar-filter"] = new(typeof(MenubarFilterFixture)),
        ["menu-dialog"] = new(typeof(MenuDialogFixture)),
        ["menu-transition"] = new(typeof(MenuTransitionFixture)),
        ["menu-triggerless"] = new(typeof(MenuTriggerlessFixture)),
        ["menu-toolbar"] = new(typeof(MenuToolbarFixture)),
        ["menu-perf"] = new(typeof(MenuPerfFixture)),
        ["menu-guards"] = new(typeof(MenuGuardsFixture)),
        ["menu-nesting"] = new(typeof(MenuNestingFixture)),
        ["menu-anchor"] = new(typeof(MenuAnchorFixture)),
        ["portal-basic"] = new(typeof(PortalBasicFixture)),
        ["portal-prerender"] = new(typeof(PortalPrerenderFixture), Prerender: true),
        ["preview-card-conformance"] = new(typeof(PreviewCardConformanceFixture)),
        ["preview-card-triggers"] = new(typeof(PreviewCardTriggersFixture)),
        ["preview-card-root"] = new(typeof(PreviewCardRootFixture)),
        ["preview-card-detached"] = new(typeof(PreviewCardDetachedFixture)),
        ["preview-card-nested"] = new(typeof(PreviewCardNestedFixture)),
        ["preview-card-inline"] = new(typeof(PreviewCardInlineFixture)),
        ["preview-card-positioner"] = new(typeof(PreviewCardPositionerFixture)),
        ["preview-card-viewport"] = new(typeof(PreviewCardViewportFixture)),
        ["scroll-lock-basic"] = new(typeof(ScrollLockBasicFixture)),
        ["positioning-suite"] = new(typeof(PositioningSuiteFixture)),
        ["positioning-origin"] = new(typeof(PositioningOriginFixture)),
        ["positioning-lifecycle"] = new(typeof(PositioningLifecycleFixture)),
        ["positioning-prerender"] = new(typeof(PositioningPrerenderFixture), Prerender: true),
        ["dialog-conformance"] = new(typeof(DialogConformanceFixture)),
        ["dialog-triggers"] = new(typeof(DialogTriggersFixture)),
        ["dialog-focus-props"] = new(typeof(DialogFocusPropsFixture)),
        ["dialog-focus-props-nonmodal"] = new(typeof(DialogFocusPropsNonModalFixture)),
        ["dialog-root"] = new(typeof(DialogRootFixture)),
        ["dialog-popup"] = new(typeof(DialogPopupFixture)),
        ["dialog-shared"] = new(typeof(DialogSharedFixture)),
        ["dialog-shadow"] = new(typeof(DialogShadowFixture)),
        ["dialog-popover"] = new(typeof(DialogPopoverFixture)),
        ["dialog-popover-nested"] = new(typeof(DialogPopoverNestedFixture)),
        ["dialog-confirmation"] = new(typeof(DialogConfirmationFixture)),
        ["alert-dialog-conformance"] = new(typeof(AlertDialogConformanceFixture)),
        ["alert-dialog-shared"] = new(typeof(AlertDialogSharedFixture)),
        ["alert-dialog-triggers"] = new(typeof(AlertDialogTriggersFixture)),
        ["alert-dialog-root"] = new(typeof(AlertDialogRootFixture)),
        ["tooltip-conformance"] = new(typeof(TooltipConformanceFixture)),
        ["tooltip-triggers"] = new(typeof(TooltipTriggersFixture)),
        ["tooltip-root"] = new(typeof(TooltipRootFixture)),
        ["tooltip-provider"] = new(typeof(TooltipProviderFixture)),
        ["tooltip-nested"] = new(typeof(TooltipNestedFixture)),
        ["tooltip-layout"] = new(typeof(TooltipLayoutFixture)),
        ["tooltip-handoff"] = new(typeof(TooltipHandoffFixture)),
        ["tooltip-positioner"] = new(typeof(TooltipPositionerFixture)),
        ["tooltip-viewport"] = new(typeof(TooltipViewportFixture)),
        ["tooltip-toolbar"] = new(typeof(TooltipToolbarFixture)),
        ["tooltip-prerender"] = new(typeof(TooltipPrerenderFixture), Prerender: true),
        ["list-values-allocation"] = new(typeof(ListValuesAllocationFixture)),
        ["list-values-collation"] = new(typeof(ListValuesCollationFixture)),
        ["list-values-indicator"] = new(typeof(ListValuesIndicatorFixture)),
        ["list-values-hidden-input"] = new(typeof(ListValuesHiddenInputFixture)),

        // M5 track 12 (issue 197): Menu's filter mode.
        ["menu-filter-root"] = new(typeof(MenuFilterRootFixture)),
        ["menu-filter-initial"] = new(typeof(MenuFilterInitialFixture)),
        ["menu-filter-submenu"] = new(typeof(MenuFilterSubmenuFixture)),

        // M5 track 1 (issue 159, Deliverables 12-13): the popup-list suite on the test-only list hosts.
        ["composite-list-dom"] = new(typeof(CompositeListDomFixture)),
        ["composite-list-virtual"] = new(typeof(CompositeListVirtualFixture)),

        // M5 track 1 (issue 159, Deliverables 6, 7, 13, 14): the non-suite list pages, the measurement fixture and the
        // ADR 01 figures' page.
        ["composite-list-nested"] = new(typeof(CompositeListNestedFixture)),
        ["composite-list-relay"] = new(typeof(CompositeListRelayFixture)),
        ["composite-list-selected"] = new(typeof(CompositeListSelectedFixture)),
        ["composite-list-measure"] = new(typeof(CompositeListMeasureFixture)),
        ["navigation-menu-basic"] = new(typeof(NavigationMenuBasicFixture)),
        ["navigation-menu-prerender"] = new(typeof(NavigationMenuPrerenderFixture), Prerender: true),

        // M5 track 9 (issue 173, Deliverable 11): Select.
        ["select-basic"] = new(typeof(SelectBasicFixture)),
        ["select-conformance"] = new(typeof(SelectConformanceFixture)),
        ["select-focus"] = new(typeof(SelectFocusFixture)),
        ["select-list"] = new(typeof(SelectListFixture)),
        ["select-positioner"] = new(typeof(SelectPositionerFixture)),
        ["select-press-item"] = new(typeof(SelectPressItemFixture)),
        ["select-press-trigger"] = new(typeof(SelectPressTriggerFixture)),
        ["select-press-indicator"] = new(typeof(SelectPressIndicatorFixture)),
        // select-press fixtures above this line (M5 track 9, L4 agent press)
        ["select-lifecycle-actions"] = new(typeof(SelectLifecycleActionsFixture)),
        ["select-lifecycle-basic"] = new(typeof(SelectLifecycleBasicFixture)),
        ["select-lifecycle-touch"] = new(typeof(SelectLifecycleTouchFixture)),
        ["select-lifecycle-arrows"] = new(typeof(SelectLifecycleArrowsFixture)),
        ["select-lifecycle-popover"] = new(typeof(SelectLifecyclePopoverFixture)),
        // select-lifecycle fixtures above this line (M5 track 9, L4 agent lifecycle)
        ["select-form-state"] = new(typeof(SelectFormStateFixture)),
        ["select-form-external"] = new(typeof(SelectFormExternalFixture)),
        ["select-form-submit"] = new(typeof(SelectFormSubmitFixture)),
        ["select-form-validation"] = new(typeof(SelectFormValidationFixture)),
        ["select-form-field"] = new(typeof(SelectFormFieldFixture)),
        ["select-form-label"] = new(typeof(SelectFormLabelFixture)),
        // select-form fixtures above this line (M5 track 9, L4 agent form)
        ["select-nav-dynamic"] = new(typeof(SelectNavDynamicFixture)),
        ["select-nav-typeahead"] = new(typeof(SelectNavTypeaheadFixture)),
        ["select-nav-multiple"] = new(typeof(SelectNavMultipleFixture)),
        ["select-nav-hover"] = new(typeof(SelectNavHoverFixture)),
        ["select-nav-groups"] = new(typeof(SelectNavGroupsFixture)),
        ["select-nav-aligned"] = new(typeof(SelectNavAlignedFixture)),
        ["select-nav-opening"] = new(typeof(SelectNavOpeningFixture)),
        ["select-nav-closing"] = new(typeof(SelectNavClosingFixture)),
        ["select-nav-arrow"] = new(typeof(SelectNavArrowFixture)),
        // select-nav fixtures above this line (M5 track 9, L4 agent nav)
        ["select-aligned-layout"] = new(typeof(SelectAlignedLayoutFixture)),
        ["select-aligned-scroll"] = new(typeof(SelectAlignedScrollFixture)),
        ["select-aligned-focus"] = new(typeof(SelectAlignedFocusFixture)),
        // select-aligned fixtures above this line (M5 track 9, L4 agent aligned)
        ["select-toolbar"] = new(typeof(SelectToolbarFixture)),
        ["select-dialog"] = new(typeof(SelectDialogFixture)),
        ["select-use-label"] = new(typeof(SelectUseLabelFixture)),
        ["field-select-prerender"] = new(typeof(FieldSelectPrerenderFixture), Prerender: true),
        // select-others fixtures above this line (M5 track 9, final review B2)
        // M5 track 10 (issue 174): Combobox.
        ["combobox-root"] = new(typeof(ComboboxRootFixture)),
        // M5 track 10 (issue 174), the Base UI titles of Combobox's [L4] rows (session M2): one block per spec file.
        // combobox.input.spec.ts
        ["combobox-input"] = new(typeof(ComboboxInputL4Fixture)),
        // combobox.trigger.spec.ts
        ["combobox-trigger"] = new(typeof(ComboboxTriggerL4Fixture)),
        // combobox.chips.spec.ts
        ["combobox-chips"] = new(typeof(ComboboxChipsL4Fixture)),
        // combobox.parts.spec.ts
        ["combobox-parts"] = new(typeof(ComboboxPartsL4Fixture)),
        // combobox.root-a.spec.ts
        ["combobox-root-a"] = new(typeof(ComboboxRootAL4Fixture)),
        // combobox.root-b.spec.ts
        ["combobox-root-b"] = new(typeof(ComboboxRootBL4Fixture)),
        // combobox.root-c.spec.ts
        ["combobox-root-c"] = new(typeof(ComboboxRootCL4Fixture)),
        // combobox.root-d.spec.ts
        ["combobox-root-d"] = new(typeof(ComboboxRootDL4Fixture)),
        // combobox.suites.spec.ts
        ["combobox-suite"] = new(typeof(ComboboxSuiteL4Fixture)),
        ["combobox-suite-list"] = new(typeof(ComboboxSuiteListL4Fixture)),
        ["combobox-suite-positioner"] = new(typeof(ComboboxSuitePositionerL4Fixture)),
        // combobox.live-regions.spec.ts, combobox.form.spec.ts (session S's batch for track 10)
        ["combobox-live-regions"] = new(typeof(ComboboxLiveRegionsL4Fixture)),
        ["combobox-form"] = new(typeof(ComboboxFormL4Fixture)),
        ["combobox-virtualize"] = new(typeof(ComboboxVirtualizeL4Fixture)),
    };
}
