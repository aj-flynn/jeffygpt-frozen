export type Theme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'jeffygpt-theme';

export function isTheme(value: string | null): value is Theme {
  return value === 'light' || value === 'dark';
}

export function getOppositeTheme(theme: Theme): Theme {
  return theme === 'light' ? 'dark' : 'light';
}

export function getSystemTheme(): Theme {
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

export function getStoredTheme(): Theme | null {
  const stored = localStorage.getItem(THEME_STORAGE_KEY);
  return isTheme(stored) ? stored : null;
}

export function resolveTheme(): Theme {
  return getStoredTheme() ?? getSystemTheme();
}

export function applyTheme(theme: Theme): void {
  document.documentElement.setAttribute('data-theme', theme);
  document.documentElement.style.colorScheme = theme;
}

export function persistTheme(theme: Theme): void {
  localStorage.setItem(THEME_STORAGE_KEY, theme);
}
