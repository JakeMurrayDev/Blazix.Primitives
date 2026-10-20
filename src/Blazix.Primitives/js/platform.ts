// Platform detection, UTL-ALG-11 (shared-utilities.md §4.16), transcribed from packages/utils/src/platform/*.ts
// at v1.9.0 along the production path (UTL-EDGE-09: the userAgentData branch is development-only upstream).
// Every flag is computed once when this module loads and is `false` without a `navigator`.

interface RawNavigatorData {
  readonly userAgent: string;
  readonly platform: string;
  readonly maxTouchPoints: number;
}

function readRawData(): RawNavigatorData {
  if (typeof navigator === 'undefined') {
    return { userAgent: '', platform: '', maxTouchPoints: 0 };
  }
  return {
    userAgent: navigator.userAgent,
    platform: navigator.platform ?? '',
    maxTouchPoints: navigator.maxTouchPoints ?? 0,
  };
}

const { userAgent, platform: rawPlatform, maxTouchPoints } = readRawData();
const lowerUserAgent = userAgent.toLowerCase();
const lowerPlatform = rawPlatform.toLowerCase();

// UTL-EDGE-01..03: the three engine flags are mutually exclusive by construction.
const webkit = typeof CSS !== 'undefined' && !!CSS.supports?.('-webkit-backdrop-filter:none');
const gecko = !webkit && lowerUserAgent.includes('firefox');
const blink = !webkit && lowerUserAgent.includes('chrom');

// UTL-EDGE-04..06.
const ios = /^i(os$|p)/.test(lowerPlatform) || (lowerPlatform === 'macintel' && maxTouchPoints > 1);
const android = lowerPlatform === 'android' || lowerUserAgent.includes('android');
const mac = !ios && lowerPlatform.startsWith('mac');
const windows = lowerPlatform.startsWith('win');
const linux = !android && /^(linux|chrome os)/.test(lowerPlatform);
const apple = mac || ios;

export const platform = {
  engine: { webkit, gecko, blink },
  os: { ios, android, mac, windows, linux, apple },
  // UTL-EDGE-07: VoiceOver *may* be running; never that it is.
  screenReader: { voiceOver: apple },
  // UTL-EDGE-08.
  env: { jsdom: /jsdom|happydom/.test(lowerUserAgent) },
  mediaQuery: { iOS: '@supports (-webkit-touch-callout: none)' },
} as const;

export type Platform = typeof platform;
