import { describe, expect, it } from 'vitest';
import { CRAP_LIMIT, crapScore, scoreSource } from './crap-score.mjs';

describe('crap score', () => {
  it('scores the threshold boundaries', () => {
    expect(CRAP_LIMIT).toBe(30);
    expect(crapScore(5, 0)).toBe(30);
    expect(crapScore(4, 0)).toBe(20);
    expect(crapScore(31, 1)).toBe(31);
    expect(crapScore(10, 0.5)).toBe(22.5);
  });

  it('counts decision points and leaves nested functions on their own score', () => {
    const choose = scoreSource(
      `export function choose(x: number, y: number) {
        if (x && y) return x ?? y;
        return y || 0;
      }`,
      'sample.ts',
      null,
    ).find((method) => method.name === 'choose');
    const kind = scoreSource(
      `export function kind(x: number) {
        switch (x) {
          case 1:
            return 'a';
          case 2:
            return 'b';
          default:
            return 'c';
        }
      }`,
      'sample.ts',
      null,
    ).find((method) => method.name === 'kind');
    const nested = scoreSource(
      `export function parent(x: number) {
        if (x) return x;
        const inner = (y: number) => {
          if (y) return y;
          return 0;
        };
        return inner(x);
      }`,
      'sample.ts',
      null,
    );

    expect(choose?.complexity).toBe(5);
    expect(kind?.complexity).toBe(3);
    expect(nested.find((method) => method.name === 'parent')?.complexity).toBe(2);
    expect(nested.find((method) => method.name === 'inner')?.complexity).toBe(2);
  });

  it('treats a missing coverage file as uncovered and statement hits as covered', () => {
    const source = `export function risky(flag: boolean) {
      if (flag) return 1;
      if (flag) return 2;
      if (flag) return 3;
      if (flag) return 4;
      return 0;
    }`;
    const uncovered = scoreSource(source, 'sample.ts', null).find(
      (method) => method.name === 'risky',
    );
    const covered = scoreSource(source, 'sample.ts', [
      { line: 2, column: 2, hits: 1 },
      { line: 3, column: 2, hits: 1 },
      { line: 4, column: 2, hits: 1 },
      { line: 5, column: 2, hits: 1 },
      { line: 6, column: 2, hits: 1 },
    ]).find((method) => method.name === 'risky');

    expect(uncovered).toMatchObject({ complexity: 5, coverage: 0, score: 30 });
    expect(covered).toMatchObject({ complexity: 5, coverage: 1, score: 5 });
  });
});
