import { afterEach, describe, expect, it } from 'vitest';
import { THEME_STORAGE_KEY, getOppositeTheme, getStoredTheme, isTheme } from './theme';

describe('theme', () => {
  afterEach(() => {
    localStorage.removeItem(THEME_STORAGE_KEY);
  });

  it('validates stored theme values', () => {
    expect(isTheme('light')).toBe(true);
    expect(isTheme('dark')).toBe(true);
    expect(isTheme('sepia')).toBe(false);
    expect(isTheme(null)).toBe(false);
  });

  it('returns opposite theme', () => {
    expect(getOppositeTheme('light')).toBe('dark');
    expect(getOppositeTheme('dark')).toBe('light');
  });

  it('reads valid theme from localStorage', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    expect(getStoredTheme()).toBe('dark');
  });

  it('returns null for invalid stored theme', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'invalid');

    expect(getStoredTheme()).toBeNull();
  });
});
