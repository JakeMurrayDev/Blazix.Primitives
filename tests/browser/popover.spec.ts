import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/popover.md (M4 track 5, issue 125) on the `popover-*` fixtures, every title on both
// render modes. The shared suites run with Popover's values (PopoverRoot.test.tsx and
// PopoverRoot.detached-triggers.test.tsx): a trigger pressed by click through the `interactions` module's `press`, role
// and aria-haspopup `dialog`, a positioner, a viewport and a close.

popupConformanceTests({
  fixture: 'popover-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
});

detachedTriggersConformanceTests({
  fixture: 'popover-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: true,
  positioner: true,
  viewport: true,
  open: {
    'allows controlling the open state and active trigger programmatically':
      'POP-API-07 (focus returns to the opener after the close, POP-FOC-05): open to PR 128, the focus manager',
  },
});

// The first run of viewportConformanceTests (track 1 wrote it for the families): PopoverViewport.test.tsx's config.
viewportConformanceTests({
  fixture: 'popover-viewport',
  openOn: 'click',
  open: {
    'shows the latest content after rapid trigger changes':
      'POP-API-14 (a trigger active again writes its payload again): open to gap 10 of track 5, Popup/PopupTriggerBehavior',
    'creates morphing containers after a kept-mounted popup closes and reopens':
      'POP-API-14 (a trigger active again writes its payload again): open to gap 10 of track 5, Popup/PopupTriggerBehavior',
  },
});
