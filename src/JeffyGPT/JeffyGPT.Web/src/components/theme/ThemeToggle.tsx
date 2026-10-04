import { useTheme } from '../../theme/ThemeProvider';
import { getOppositeTheme } from '../../theme/theme';
import './ThemeToggle.css';

export function ThemeToggle() {
  const { theme, toggleTheme } = useTheme();
  const oppositeTheme = getOppositeTheme(theme);

  return (
    <button
      type="button"
      className="theme-toggle"
      onClick={toggleTheme}
      aria-label={`Switch to ${oppositeTheme} theme`}
      title={`Switch to ${oppositeTheme} theme`}
    >
      {theme === 'light' ? (
        <span className="theme-toggle__icon" aria-hidden="true">
          ☾
        </span>
      ) : (
        <span className="theme-toggle__icon" aria-hidden="true">
          ☀
        </span>
      )}
    </button>
  );
}
