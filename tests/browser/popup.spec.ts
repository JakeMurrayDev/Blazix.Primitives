import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';

// The [L4] rows of docs/architecture/popup-store-and-handles.md (PSH) and transitions-and-mounting.md (TRN) on the
// test-only popup host (FixturePopupRoot, Trigger, Portal, Popup and Close; settled by the orchestrator 2026-10-27, plan
// §Settled "Test-only hosts and the shared suites"). The shared suites run here with the host's values: a trigger pressed
// by click from a C# onclick (design point 4), role and aria-haspopup `dialog`, an exit animation. Every test runs on both
// render modes. Titles that press Escape are DIS-01's, proven by track 3's `interactions` module.

const escape = 'PSH-DIS-01 (Escape): open to M4 track 3, the interactions module; the popup host has no dismissal';

popupConformanceTests({
  fixture: 'popup-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'dialog',
  open: {
    'opens initially and remains uncontrolled': escape,
    'is ignored when `open={true}` is passed, so Escape cannot close': escape,
    'is called with the new open state and the change details': escape,
  },
});

detachedTriggersConformanceTests({
  fixture: 'popup-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: true,
  open: {
    'allows controlling the open state and active trigger programmatically':
      'PSH-FOC (focus returns to the opener after the close): open to M4 track 4, the focus manager',
  },
});
