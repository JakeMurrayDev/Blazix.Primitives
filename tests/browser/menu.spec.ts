import { detachedTriggersConformanceTests } from './suites/detachedTriggersConformanceTests';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { popupFocusPropsTests } from './suites/popupFocusPropsTests';
import { popupListConformanceTests } from './suites/popupListConformanceTests';
import { positionerConformanceTests } from './suites/positioner-conformance';
import { viewportConformanceTests } from './suites/viewportConformanceTests';

// The [L4] rows of docs/components/menu.md (M5 track 6, issue 172) on the `menu-*` fixtures, every title on both render
// modes. The shared popup suites run with Menu's values (MenuRoot.test.tsx:127-143, MenuRoot.detached-triggers.test.tsx:14-26,
// MenuViewport.test.tsx:25-46, MenuPopup.test.tsx:199-215, MenuPositioner.test.tsx:633-648): a trigger pressed by click
// (the `interactions` module toggles on its `mousedown`), the popup's role and the trigger's aria-haspopup `menu`, no close
// part, a positioner and a viewport.

popupConformanceTests({
  fixture: 'menu-conformance',
  triggerMouseAction: 'click',
  expectedPopupRole: 'menu',
});

detachedTriggersConformanceTests({
  fixture: 'menu-triggers',
  openInteractions: ['click'],
  ariaExpanded: true,
  throwOnMissingTrigger: true,
  close: false,
  positioner: true,
  viewport: true,
});

viewportConformanceTests({
  fixture: 'menu-viewport',
  openOn: 'click',
});

positionerConformanceTests({
  fixture: 'menu-positioner',
  viewport: true,
});

popupFocusPropsTests({
  fixture: 'menu-focus',
  initialFocus: false,
});

// MenuRoot.test.tsx:267-294: the popup-list suite with Menu's options, on Menu's own parts (track 1 ran it on its test-only
// host, composite-list.spec.ts:13).
popupListConformanceTests({
  fixture: 'menu-list',
  itemRole: 'menuitem',
  listEnd: 'wrap',
});
