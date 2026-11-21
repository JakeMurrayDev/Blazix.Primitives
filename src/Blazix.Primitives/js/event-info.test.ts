// L3 (ADR 06 §1): the event snapshot's `cancelable` (M6 track 8c, issue 279, added 2026-11-21; design point D3 (b), chosen
// by the user 2026-11-21). DRW-DIS-01's close watcher reads `details.event.cancelable`; the C# half is EventSnapshot.
import { describe, expect, it } from 'vitest';
import { eventInfo, placeholderEvent } from './event-info';

describe('eventInfo', () => {
  it('port: records whether the event was cancelable, which the placeholder is not', () => {
    expect(eventInfo(new Event('cancel', { cancelable: true })).cancelable).toBe(true);
    expect(eventInfo(new Event('cancel', { cancelable: false })).cancelable).toBe(false);
    expect(placeholderEvent().cancelable).toBe(false);
  });
});
