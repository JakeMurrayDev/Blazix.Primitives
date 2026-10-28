import type { Page } from '@playwright/test';

/**
 * Adds a one-way delay to the Blazor Server circuit's WebSocket in each direction (ADR 06 §2: CDP network emulation
 * does not delay it). Messages keep their order in each direction.
 */
export async function delayCircuit(page: Page, oneWayMs: number): Promise<void> {
  await page.routeWebSocket(/_blazor/, (socket) => {
    const server = socket.connectToServer();
    socket.onMessage((message) => {
      setTimeout(() => server.send(message), oneWayMs);
    });
    server.onMessage((message) => {
      setTimeout(() => socket.send(message), oneWayMs);
    });
  });
}
