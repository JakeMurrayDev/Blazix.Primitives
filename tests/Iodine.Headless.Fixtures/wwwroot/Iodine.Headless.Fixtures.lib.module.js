// The fixture host's JS initializer (ADR 06 §2): the readiness marker, the harness defaults, and the page hook the
// tests reach the per-page session through (tests/browser/helpers/session.ts).

const sessions = new Map();

function session(token) {
  const reference = sessions.get(token);
  if (!reference) {
    throw new Error(`iodineFixture: no session registered for token ${token}`);
  }
  return reference;
}

// A node a consumer's script holds and C# receives as an IJSObjectReference (the portal's `container`; Dialog's titles):
// a closed shadow root, which no selector or element reference reaches. The last one is kept for the tests to read. An
// open one (Popover's shadow-root titles, as Base UI's tests attach it) is reached by Playwright's locators.
globalThis.iodineFixtureDom = {
  createClosedShadowRoot(host) {
    const root = host.attachShadow({ mode: 'closed' });
    globalThis.iodineFixtureDom.lastClosedShadowRoot = root;
    return root;
  },
  createOpenShadowRoot(host) {
    return host.attachShadow({ mode: 'open' });
  },
  // Issue 316 (toast.viewport.spec.ts's owner-document title): a same-origin iframe inside `host`, and a container in its
  // body for a portal. The frame window's and document's listener calls are recorded in `iframeListenerCalls`
  // (`+window:keydown`, `-document:pointerdown`).
  createIframeContainer(host) {
    const iframe = host.ownerDocument.createElement('iframe');
    iframe.setAttribute('data-testid', 'toast-iframe');
    host.appendChild(iframe);
    const calls = [];
    globalThis.iodineFixtureDom.iframeListenerCalls = calls;
    for (const [target, name] of [[iframe.contentWindow, 'window'], [iframe.contentDocument, 'document']]) {
      const add = target.addEventListener.bind(target);
      const remove = target.removeEventListener.bind(target);
      target.addEventListener = (type, listener, options) => {
        calls.push(`+${name}:${type}`);
        add(type, listener, options);
      };
      target.removeEventListener = (type, listener, options) => {
        calls.push(`-${name}:${type}`);
        remove(type, listener, options);
      };
    }
    const container = iframe.contentDocument.createElement('div');
    iframe.contentDocument.body.appendChild(container);
    return container;
  },
  iframeListenerCalls: [],
  lastClosedShadowRoot: null,
  // M5 track 1's virtual list host (issue 159, Deliverable 12): the Combobox family's Enter on its input
  // (ComboboxInput.tsx:404-420 at Base UI v1.9.0), which no module of the list mechanism owns: while the input is expanded
  // and names an active descendant, Enter clicks that item and is stopped, so a surrounding form is not submitted.
  listenComboboxEnter(input) {
    input.addEventListener('keydown', (event) => {
      if (event.key !== 'Enter' || event.which === 229 || input.getAttribute('aria-expanded') !== 'true') {
        return;
      }
      const id = input.getAttribute('aria-activedescendant');
      const item = id ? input.ownerDocument.getElementById(id) : null;
      if (item) {
        event.preventDefault();
        event.stopPropagation();
        item.click();
      }
    });
  },
};

globalThis.iodineFixture = {
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
  document.documentElement.setAttribute('data-iodine-started', '');
}
