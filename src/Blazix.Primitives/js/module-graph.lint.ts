// Skeleton for the red run.
export type ModuleKind = 'mechanism' | 'component' | 'shared';
export const moduleKinds: Record<string, ModuleKind> = {};
export function extractImports(_source: string): string[] {
  return [];
}
export function checkGraph(_modules: { name: string; imports: string[] }[], _kinds: Record<string, ModuleKind>): string[] {
  return [];
}
export function readModules(_directory: string): { name: string; imports: string[] }[] {
  return [];
}
