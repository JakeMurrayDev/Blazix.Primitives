import { test } from './helpers';
import { popupListConformanceTests } from './suites/popupListConformanceTests';

// The popup-list suite on M5 track 1's test-only list hosts (issue 159, Deliverable 13; fact sheet A Q10 (a), the M4
// precedent): Base UI runs popupListConformanceTests from MenuRoot.test.tsx, SelectRoot.test.tsx and
// ComboboxRoot.test.tsx with the options below, and each family's list options are those of its useListNavigation and
// useTypeahead call sites (fact sheet A §3: MenuRoot.tsx:516-568, SelectRoot.tsx:371-411, AriaCombobox.tsx:1416-1449).
// Menu and Select run on `composite-list-dom` (a button reference, DOM focus), Combobox on `composite-list-virtual` (an
// input reference, virtual focus). A title listed open names the family behaviour the host lacks and the track that
// proves it.

test.describe('Menu', () => {
  popupListConformanceTests({
    fixture: 'composite-list-dom',
    // MenuRoot.tsx:523 (loopFocus, Root default true), :555-568 (TYPEAHEAD_RESET_MS 500, elementsRef: skip by element).
    query: { loopFocus: true, typeahead: true, typeaheadResetMs: 500, typeaheadSkip: 'element' },
    itemRole: 'menuitem',
    listEnd: 'wrap',
  });
});

test.describe('Select', () => {
  popupListConformanceTests({
    fixture: 'composite-list-dom',
    // SelectRoot.tsx:371-386 (no loopFocus), :388-411 (default 750, isElementDisabled: skip by attribute).
    query: { typeahead: true, typeaheadSkip: 'attribute' },
    itemRole: 'option',
    selectable: true,
    listEnd: 'stop',
  });
});

test.describe('Combobox', () => {
  popupListConformanceTests({
    fixture: 'composite-list-virtual',
    // AriaCombobox.tsx:1422-1424 (virtual, loopFocus default true, allowEscape = loopFocus && !autoHighlight); no
    // typeahead on an open list.
    query: { loopFocus: true, allowEscape: true },
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
});
