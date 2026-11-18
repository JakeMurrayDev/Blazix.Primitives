// Red stub (M6 track 1): the exports are present and ignore the requested timeStamp; the next commit replaces it.
export type PointerInit = PointerEventInit & { timeStamp: number };

function fire(type: string, element: Element, init: PointerInit): boolean {
  const { timeStamp, ...eventInit } = init;
  void timeStamp;
  return element.dispatchEvent(new PointerEvent(type, eventInit));
}

export const firePointer = {
  down: (element: Element, init: PointerInit) => fire('pointerdown', element, init),
  move: (element: Element, init: PointerInit) => fire('pointermove', element, init),
  up: (element: Element, init: PointerInit) => fire('pointerup', element, init),
};

export function pressWithTouch(element: Element): void {
  void element;
}
