// The fixture host's JS initializer (ADR 06 §2): the readiness marker, the harness defaults, and the page hook the
// tests reach the per-page session through (tests/browser/helpers/session.ts).

const sessions = new Map();

function session(token) {
  const reference = sessions.get(token);
  if (!reference) {
    throw new Error(`blazixFixture: no session registered for token ${token}`);
  }
  return reference;
}

// A node a consumer's script holds and C# receives as an IJSObjectReference (the portal's `container`; Dialog's titles):
// a closed shadow root, which no selector or element reference reaches. The last one is kept for the tests to read.
globalThis.blazixFixtureDom = {
  createClosedShadowRoot(host) {
    const root = host.attachShadow({ mode: 'closed' });
    globalThis.blazixFixtureDom.lastClosedShadowRoot = root;
    return root;
  },
  lastClosedShadowRoot: null,
};

globalThis.blazixFixture = {
  /** Called by FixtureContainer on its first interactive render with the session's DotNetObjectReference. */
  register(token, reference) {
    sessions.set(token, reference);
  },
  /** Advances the session's C# TimeProvider on the renderer's dispatcher (both render modes). */
  advanceTime(token, milliseconds) {
    return session(token).invokeMethodAsync('AdvanceTime', milliseconds);
  },
  /** The session's captured diagnostics (the WebAssembly channel). */
  async diagnostics(token) {
    return JSON.parse(await session(token).invokeMethodAsync('DiagnosticsJson'));
  },
};

export function beforeWebStart(options) {
  // Animations off by default (TRN-EDGE-01; the switch itself is the transitions module, M1); ?animations=1 turns them on.
  globalThis.BASE_UI_ANIMATIONS_DISABLED = new URLSearchParams(location.search).get('animations') !== '1';

  // Mirror the host's hub options (Program.cs) so the client pings within the server's ClientTimeoutInterval.
  options.circuit ??= {};
  options.circuit.configureSignalR = (builder) => builder.withKeepAliveInterval(2000).withServerTimeout(10000);
}

export function afterWebStarted() {
  document.documentElement.setAttribute('data-blazix-started', '');
}
