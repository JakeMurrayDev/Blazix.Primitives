/// <reference types="node" />
import { defineConfig } from '@playwright/test';
import type { RenderMode } from './tests/browser/helpers/test';

type Options = { mode: RenderMode };

// L4 (ADR 06 §2 as amended 2026-10-20, ADR 11 §2 and §4): two projects, one per render mode, against one fixture
// host started by `webServer`. Readiness is the host's endpoint and the page markers; timeouts are ceilings.
const host = 'http://127.0.0.1:5297';
const inCi = !!process.env.CI;

export default defineConfig<Options>({
  testDir: 'tests/browser',
  fullyParallel: true,
  // ADR 11 §4: no retries in the gate, and enabling them anywhere still fails the run.
  retries: 0,
  failOnFlakyTests: true,
  forbidOnly: inCi,
  ...(inCi ? { workers: 2 } : {}),
  reporter: inCi ? [['blob'], ['github']] : [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: host,
    trace: 'retain-on-failure',
    // The host sets BASE_UI_ANIMATIONS_DISABLED by default (ADR 06 §2); the switch itself is the transitions module (M1).
  },
  webServer: {
    command: 'dotnet run --project tests/Blazix.Primitives.Fixtures --no-launch-profile',
    url: `${host}/_fixture/ready`,
    reuseExistingServer: !inCi,
    timeout: Number(process.env.BLAZIX_WEBSERVER_TIMEOUT ?? 240_000),
    stdout: 'ignore',
    stderr: 'pipe',
    env: {
      ASPNETCORE_URLS: host,
      ASPNETCORE_ENVIRONMENT: 'Development',
      DOTNET_NOLOGO: '1',
    },
  },
  projects: [
    {
      name: 'server',
      testIgnore: /wasm-warmup\.setup\.ts/,
      use: { mode: 'server' },
      expect: { timeout: 5_000 },
      timeout: 30_000,
    },
    {
      // Opens one WebAssembly page before the wasm project so the runtime download and the host's asset pipeline
      // are paid once per run, and so a broken WebAssembly boot fails fast with one message (ADR 06 §2).
      name: 'wasm-warmup',
      testMatch: /wasm-warmup\.setup\.ts/,
      use: { mode: 'wasm' },
      expect: { timeout: 60_000 },
      timeout: 120_000,
    },
    {
      name: 'wasm',
      dependencies: ['wasm-warmup'],
      testIgnore: /wasm-warmup\.setup\.ts/,
      use: { mode: 'wasm' },
      expect: { timeout: 15_000 },
      timeout: 60_000,
    },
  ],
});
