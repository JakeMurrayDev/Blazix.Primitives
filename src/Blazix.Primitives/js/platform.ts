// Platform detection, transcribed from Base UI's packages/utils/src/platform/*.ts at v1.9.0 along the production
// path (the userAgentData branch is development-only upstream): UTL-ALG-11, UTL-EDGE-01..09.
// The page's flags are computed once when this module loads and are all `false` without a `navigator`. The `dom`
// module owns them (ADR 04 §1) and re-exports this module; the initializer imports it so the chunk loads early.

/** The `navigator` fields the flags are computed from. */
export interface NavigatorData {
  readonly userAgent: string;
  readonly platform: string;
  readonly maxTouchPoints: number;
}

/** Booleans that describe the browser, computed once per page. */
export interface Platform {
  readonly engine: { readonly webkit: boolean; readonly gecko: boolean; readonly blink: boolean };
  readonly os: {
    readonly ios: boolean;
    readonly android: boolean;
    readonly mac: boolean;
    readonly windows: boolean;
    readonly linux: boolean;
    readonly apple: boolean;
  };
  /** `voiceOver` means VoiceOver *may* be running, never that it is. */
  readonly screenReader: { readonly voiceOver: boolean };
  readonly env: { readonly jsdom: boolean };
  readonly mediaQuery: { readonly iOS: string };
}

function deepFreeze<T extends object>(value: T): T {
  for (const nested of Object.values(value)) {
    if (typeof nested === 'object' && nested !== null) {
      deepFreeze(nested);
    }
  }
  return Object.freeze(value);
}

/**
 * Computes the platform flags.
 * @param data The user agent, platform string and touch points of `navigator`, or `undefined` where there is no
 * `navigator`.
 * @param webkitBackdrop Whether `CSS.supports('-webkit-backdrop-filter:none')` holds, which only WebKit answers yes to.
 * @returns The flags, frozen.
 */
export function detectPlatform(data: NavigatorData | undefined, webkitBackdrop: boolean): Platform {
  const userAgent = (data?.userAgent ?? '').toLowerCase();
  const platformString = (data?.platform ?? '').toLowerCase();
  const maxTouchPoints = data?.maxTouchPoints ?? 0;

  // The three engine flags are mutually exclusive by construction.
  const webkit = webkitBackdrop;
  const gecko = !webkit && userAgent.includes('firefox');
  const blink = !webkit && userAgent.includes('chrom');

  const ios = /^i(os$|p)/.test(platformString) || (platformString === 'macintel' && maxTouchPoints > 1);
  const android = platformString === 'android' || userAgent.includes('android');
  const mac = !ios && platformString.startsWith('mac');
  const windows = platformString.startsWith('win');
  const linux = !android && /^(linux|chrome os)/.test(platformString);
  const apple = mac || ios;

  return deepFreeze({
    engine: { webkit, gecko, blink },
    os: { ios, android, mac, windows, linux, apple },
    screenReader: { voiceOver: apple },
    env: { jsdom: /jsdom|happydom/.test(userAgent) },
    mediaQuery: { iOS: '@supports (-webkit-touch-callout: none)' },
  });
}

function readNavigator(): NavigatorData | undefined {
  if (typeof navigator === 'undefined') {
    return undefined;
  }
  return {
    userAgent: navigator.userAgent,
    platform: navigator.platform ?? '',
    maxTouchPoints: navigator.maxTouchPoints ?? 0,
  };
}

/** The flags of this page. */
export const platform: Platform = detectPlatform(
  readNavigator(),
  typeof CSS !== 'undefined' && !!CSS.supports?.('-webkit-backdrop-filter:none'),
);
