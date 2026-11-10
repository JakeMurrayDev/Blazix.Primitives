// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { budgetClasses, exclusionArgs, summarize } from './allocation-budgets.mjs';

// ADR 16 Amendments 2026-11-11 (issue 216): the main `dotnet test` runs skip the classes of the `AllocationBudgets`
// collection, and the script runs each of them in a process of its own. These cases cover the pure half: finding the
// classes in C# source, the exclusion arguments, and the summary's verdict.
describe('budget classes', () => {
  it('names a class in the collection by its file-scoped namespace', () => {
    const source = [
      'namespace Blazix.Primitives.Tests.Components;',
      '',
      '/// <summary>A budget.</summary>',
      '[Collection(AllocationBudgetsCollection.Name)]',
      'public sealed class ToggleAllocationBudgetTests : IDisposable',
      '{',
      '}',
    ].join('\n');

    expect(budgetClasses(source)).toEqual(['Blazix.Primitives.Tests.Components.ToggleAllocationBudgetTests']);
  });

  it('names a class under a block namespace, after other attributes, and by the collection\'s literal name', () => {
    const source = [
      'namespace Blazix.Primitives.Tests.Rendering',
      '{',
      '    [Collection("AllocationBudgets")]',
      '    [Trait("Layer", "L1")]',
      '    internal sealed class AllocationBudgetTests',
      '    {',
      '    }',
      '}',
    ].join('\n');

    expect(budgetClasses(source)).toEqual(['Blazix.Primitives.Tests.Rendering.AllocationBudgetTests']);
  });

  it('leaves out the classes of other collections and the collection\'s own definition', () => {
    const source = [
      'namespace Blazix.Primitives.Tests.Components;',
      '',
      '[CollectionDefinition(Name, DisableParallelization = true)]',
      'public sealed class AllocationBudgetsCollection',
      '{',
      '    public const string Name = "AllocationBudgets";',
      '}',
      '',
      '[Collection("Interop")]',
      'public sealed class InteropTests',
      '{',
      '}',
      '',
      '[Collection(AllocationBudgetsCollection.Name)]',
      'public sealed class FocusManagerBehaviorTests',
      '{',
      '}',
    ].join('\n');

    expect(budgetClasses(source)).toEqual(['Blazix.Primitives.Tests.Components.FocusManagerBehaviorTests']);
  });

  it('finds no class in a file without the attribute', () => {
    expect(budgetClasses('namespace A;\n\npublic sealed class Plain\n{\n}\n')).toEqual([]);
  });
});

describe('exclusion arguments', () => {
  it('passes one --filter-not-class per budget class, and nothing for a project without budgets', () => {
    expect(exclusionArgs(['A.One', 'B.Two'])).toEqual(['--filter-not-class', 'A.One', '--filter-not-class', 'B.Two']);
    expect(exclusionArgs([])).toEqual([]);
  });
});

describe('summary', () => {
  const result = (name, status, seconds = 2) => ({ project: 'tests/Blazix.Primitives.Tests.Components', name, status, seconds });

  it('passes when every class passed, and gives the total time', () => {
    const { ok, markdown } = summarize([result('A.One', 0, 2.5), result('B.Two', 0, 3)]);

    expect(ok).toBe(true);
    expect(markdown).toContain('All 2 budget classes passed, one process each (6 s).');
    expect(markdown).toContain('| tests/Blazix.Primitives.Tests.Components | `A.One` | passed | 2.5 |');
  });

  it('fails on a failed class and on a class whose filter ran no test', () => {
    const { ok, markdown } = summarize([result('A.One', 0), result('B.Two', 2), result('C.Three', 8)]);

    expect(ok).toBe(false);
    expect(markdown).toContain('2 of 3 budget classes did not pass.');
    expect(markdown).toContain('| `B.Two` | failed |');
    expect(markdown).toContain('| `C.Three` | ran no test |');
  });

  it('fails when no class was found', () => {
    expect(summarize([]).ok).toBe(false);
  });
});
