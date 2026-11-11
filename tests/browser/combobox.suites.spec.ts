import { test } from './helpers';
import { popupConformanceTests } from './suites/popupConformanceTests';
import { popupListConformanceTests } from './suites/popupListConformanceTests';
import { positionerConformanceTests } from './suites/positioner-conformance';

// Base UI's shared suites as ComboboxRoot.test.tsx runs them on Combobox's own parts (M5 track 10, issue 174; the rows that
// cite them in docs/components/combobox.md): `composite-list.spec.ts` runs the popup-list suite's Combobox options on track
// 1's test-only host, and this file runs both suites on the real Root, Input, Portal, Positioner, Popup, List and Items.

test.describe('Combobox', () => {
  // ComboboxRoot.test.tsx's popupConformanceTests call: the Input is the trigger, the `popup` props go to the List (it
  // carries the listbox role), which has no exit state.
  popupConformanceTests({
    fixture: 'combobox-suite',
    triggerMouseAction: 'click',
    expectedPopupRole: 'listbox',
    combobox: true,
    openReason: 'input-press',
    exitAnimation: false,
  });

  // ComboboxRoot.test.tsx's popupListConformanceTests call, with Base UI's options.
  popupListConformanceTests({
    fixture: 'combobox-suite-list',
    itemRole: 'option',
    focusModel: 'virtual',
    homeEnd: false,
    selectable: true,
    spaceActivates: false,
    enterSpaceOpen: false,
    typeahead: false,
    modal: false,
    triggerClickCloses: false,
    listEnd: 'escape',
  });

  // ComboboxPositioner.test.tsx's positionerConformanceTests call: the Input is the anchor; Combobox has no Viewport.
  positionerConformanceTests({
    fixture: 'combobox-suite-positioner',
  });
});
