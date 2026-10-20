// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { classify, familyFolderName, formatOutputs } from './classify-paths.mjs';

// ADR 11 section 1: which L4 specs a pull request runs. The family list is the stems of docs/components/*.md and the
// spec list is the files of tests/browser at the PR's head; both are inputs so the rule is testable without a checkout.
const families = ['tabs', 'alert-dialog', 'checkbox', 'checkbox-group', 'button', 'otp-field'];
const specFiles = [
  'tests/browser/alert-dialog.spec.ts',
  'tests/browser/checkbox-group.spec.ts',
  'tests/browser/checkbox.spec.ts',
  'tests/browser/harness.spec.ts',
  'tests/browser/smoke.spec.ts',
  'tests/browser/tabs.indicator.spec.ts',
  'tests/browser/tabs.spec.ts',
];

const run = (changed, extra = {}) => classify({ changed, labels: [], event: 'pull_request', families, specFiles, ...extra });

describe('family folder names', () => {
  it('turns a document stem into the PascalCase folder of ADR 08', () => {
    expect(familyFolderName('tabs')).toBe('Tabs');
    expect(familyFolderName('alert-dialog')).toBe('AlertDialog');
    expect(familyFolderName('otp-field')).toBe('OtpField');
  });
});

describe('scoped L4', () => {
  it('a change confined to one family folder runs that family\'s specs in both modes', () => {
    const result = run(['src/Blazix.Primitives/Tabs/TabsRoot.cs', 'src/Blazix.Primitives/Tabs/TabsList.cs']);
    expect(result.l4).toBe('scoped');
    expect(result.families).toEqual(['tabs']);
    expect(result.specs).toEqual(['tests/browser/tabs.indicator.spec.ts', 'tests/browser/tabs.spec.ts']);
    expect(result.runL4).toBe(true);
  });

  it('a change confined to one family\'s spec files runs those specs', () => {
    const result = run(['tests/browser/alert-dialog.spec.ts']);
    expect(result.l4).toBe('scoped');
    expect(result.families).toEqual(['alert-dialog']);
    expect(result.specs).toEqual(['tests/browser/alert-dialog.spec.ts']);
  });

  it('matches a spec to its family by the name before the first dot, so checkbox does not claim checkbox-group', () => {
    expect(run(['tests/browser/checkbox.spec.ts']).specs).toEqual(['tests/browser/checkbox.spec.ts']);
    expect(run(['src/Blazix.Primitives/CheckboxGroup/CheckboxGroupRoot.cs']).specs).toEqual(['tests/browser/checkbox-group.spec.ts']);
  });

  it('several families run all of their specs', () => {
    const result = run(['src/Blazix.Primitives/Tabs/TabsRoot.cs', 'src/Blazix.Primitives/AlertDialog/AlertDialogRoot.cs']);
    expect(result.l4).toBe('scoped');
    expect(result.families).toEqual(['alert-dialog', 'tabs']);
    expect(result.specs).toEqual(['tests/browser/alert-dialog.spec.ts', 'tests/browser/tabs.indicator.spec.ts', 'tests/browser/tabs.spec.ts']);
  });

  it('a harness spec runs by itself', () => {
    const result = run(['tests/browser/harness.spec.ts']);
    expect(result.l4).toBe('scoped');
    expect(result.families).toEqual([]);
    expect(result.specs).toEqual(['tests/browser/harness.spec.ts']);
  });

  it('a family with no spec yet is scoped and has nothing to run', () => {
    const result = run(['src/Blazix.Primitives/Button/Button.cs']);
    expect(result.l4).toBe('scoped');
    expect(result.specs).toEqual([]);
    expect(result.runL4).toBe(false);
  });

  it('files that cannot change a browser result do not widen a family change', () => {
    const result = run([
      'src/Blazix.Primitives/Tabs/TabsRoot.cs',
      'tests/Blazix.Primitives.Tests/Tabs/TabsRootTests.cs',
      'tests/Blazix.Primitives.Tests.Components/Tabs/TabsRootRenderTests.cs',
      'src/Blazix.Primitives/Tabs/CONTEXT.md',
    ]);
    expect(result.l4).toBe('scoped');
    expect(result.families).toEqual(['tabs']);
  });
});

describe('full L4', () => {
  it.each([
    'src/Blazix.Primitives/Rendering/PartBase.cs',
    'src/Blazix.Primitives/Store/Store.cs',
    'src/Blazix.Primitives/Events/ChangeEventDetails.cs',
    'src/Blazix.Primitives/Interop/TwinBase.cs',
    'src/Blazix.Primitives/js/twin.ts',
    'src/Blazix.Primitives/Blazix.Primitives.csproj',
    'src/Blazix.Primitives.Generators/PartStateGenerator.cs',
    'tests/Blazix.Primitives.Fixtures/Program.cs',
    'tests/Blazix.Primitives.Fixtures.Client/FixtureCatalog.cs',
    'tests/browser/helpers/navigation.ts',
    'tests/browser/wasm-warmup.setup.ts',
    'playwright.config.ts',
    'package.json',
    'package-lock.json',
    'global.json',
    'Directory.Build.props',
    'Directory.Packages.props',
    '.editorconfig',
    '.github/workflows/ci.yml',
    '.github/actions/browser-tests/action.yml',
  ])('a shared path forces it: %s', (path) => {
    const result = run([path]);
    expect(result.l4).toBe('full');
    expect(result.specs).toEqual([]);
    expect(result.runL4).toBe(true);
  });

  it('a path nobody classified forces it, so a new folder is never silently skipped', () => {
    expect(run(['src/Blazix.Primitives/Frobnicate/Frob.cs']).l4).toBe('full');
  });

  it('a family folder and a shared folder together are full L4 (the synthetic pull request of issue 22)', () => {
    const result = run(['src/Blazix.Primitives/Tabs/TabsRoot.cs', 'src/Blazix.Primitives/Rendering/PartBase.cs']);
    expect(result.l4).toBe('full');
    expect(result.runL4).toBe(true);
  });

  it('a family spec and a shared helper together are full L4', () => {
    expect(run(['tests/browser/tabs.spec.ts', 'tests/browser/helpers/test.ts']).l4).toBe('full');
  });

  it('the full-l4 label forces it, even on a documents-only change', () => {
    const result = run(['docs/README.md'], { labels: ['full-l4'] });
    expect(result.l4).toBe('full');
    expect(result.docs).toBe(true);
    expect(result.code).toBe(false);
  });

  it('other labels change nothing', () => {
    expect(run(['docs/README.md'], { labels: ['verification', 'track'] }).l4).toBe('none');
  });

  it('a push to main runs everything, whatever changed', () => {
    const result = run(['docs/README.md'], { event: 'push' });
    expect(result.l4).toBe('full');
    expect(result.docs).toBe(true);
  });
});

describe('no L4', () => {
  it('a documents-only change runs only docs-lint', () => {
    const result = run(['docs/components/tabs.md', 'docs/decisions/11-continuous-integration.md']);
    expect(result).toMatchObject({ docs: true, code: false, l4: 'none', runL4: false, specs: [] });
  });

  it('a change confined to the L1, L2 and generator test projects needs no browser run', () => {
    const result = run([
      'tests/Blazix.Primitives.Tests/Store/StoreTests.cs',
      'tests/Blazix.Primitives.Tests.Components/Rendering/RenderPipelineTests.cs',
      'tests/Blazix.Primitives.Tests.Generators/PartState/PartStateGeneratorTests.cs',
    ]);
    expect(result).toMatchObject({ docs: false, code: true, l4: 'none', runL4: false });
  });

  it('a Markdown file outside docs/ still counts as code for build-and-test but not for L4', () => {
    expect(run(['README.md'])).toMatchObject({ docs: false, code: true, l4: 'none' });
  });

  it('an empty change set has nothing to run', () => {
    expect(run([])).toMatchObject({ docs: false, code: false, l4: 'none', runL4: false });
  });

  it('documents and a family change together run docs-lint and the family\'s specs', () => {
    const result = run(['docs/components/tabs.md', 'src/Blazix.Primitives/Tabs/TabsRoot.cs']);
    expect(result).toMatchObject({ docs: true, code: true, l4: 'scoped', families: ['tabs'] });
  });
});

describe('workflow outputs', () => {
  it('writes the keys the workflow reads, with specs space-separated and empty for full L4', () => {
    const scoped = formatOutputs(run(['src/Blazix.Primitives/Tabs/TabsRoot.cs']));
    expect(scoped).toBe(
      [
        'docs=false',
        'code=true',
        'l4=scoped',
        'run-l4=true',
        'families=tabs',
        'specs=tests/browser/tabs.indicator.spec.ts tests/browser/tabs.spec.ts',
      ].join('\n') + '\n',
    );
    const full = formatOutputs(run(['playwright.config.ts']));
    expect(full).toContain('l4=full\n');
    expect(full).toContain('specs=\n');
  });
});
