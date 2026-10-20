// The probe module: the harness canary of issue 33 (deliverable 6). Test-only; it ships with the fixtures, not the
// library, and exercises every edge of the twin contract (ADR 04 §3) the way a real module would.
import { decide } from '../../../src/Blazix.Primitives/js/decide';
import {
  applyConsumerStyle,
  createTwinChannel,
  parseOptions,
  resolveElement,
  type DotNetTwin,
  type TwinHandle,
  type TwinOptions,
} from '../../../src/Blazix.Primitives/js/twin';

interface ProbeOptions extends TwinOptions {
  label: string;
}

export function attach(elementOrId: Element | string, optionsJson: string, twinReference: DotNetTwin): TwinHandle {
  const element = resolveElement(elementOrId);
  const twin = createTwinChannel(twinReference);
  let options = parseOptions<ProbeOptions>(optionsJson);
  let count = 0;
  let disposed = false;

  const apply = () => {
    applyConsumerStyle(element, options);
    element.setAttribute('data-probe-label', options.label);
  };

  const onClick = (event: MouseEvent) => {
    // The container renders the C# decision count; comparing it before and after decide() shows whether the render
    // queued inside the synchronous invoke reached the DOM before this listener returns (ADR-04/2).
    const container = element.closest('[data-fixture]');
    const before = container?.getAttribute('data-decisions');
    const verdict = decide(twin, 'activate', event, 'run');
    const after = container?.getAttribute('data-decisions');
    element.setAttribute('data-verdict', verdict);
    element.setAttribute('data-verdict-source', twin.lastDecisionWasSynchronous ? 'twin' : 'fallback');
    element.setAttribute('data-flushed-in-dispatch', String(twin.lastDecisionWasSynchronous && before !== after));
    if (verdict === 'run') {
      count += 1;
      twin.report('OnClick', { source: 'js-click', count });
    }
  };

  apply();
  element.addEventListener('click', onClick);
  element.setAttribute('data-probe-attached', 'true');

  return {
    update(nextOptionsJson: string) {
      if (disposed) {
        return;
      }
      options = parseOptions<ProbeOptions>(nextOptionsJson);
      apply();
    },
    dispose() {
      if (disposed) {
        return;
      }
      disposed = true;
      element.removeEventListener('click', onClick);
      element.setAttribute('data-probe-attached', 'false');
    },
  };
}
