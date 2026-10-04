import { defineConfig, type Plugin } from 'vite';
import react from '@vitejs/plugin-react';

const apiTarget =
  process.env.services__api__http__0 ?? process.env.API_HTTP ?? 'http://localhost:5008';

const port = Number(process.env.PORT) || 5173;
const underAspire = Boolean(process.env.PORT);

/**
 * Aspire often launches Vite with NODE_ENV=production, which can skip @vitejs/plugin-react's
 * HTML preamble while JSX still emits $RefreshReg$/$RefreshSig$ — blank screen.
 * Always inject the preamble during `vite` serve (no-op when already present).
 */
function ensureReactRefreshPreamble(): Plugin {
  const preamble = `import { injectIntoGlobalHook } from "/@react-refresh";
injectIntoGlobalHook(window);
window.$RefreshReg$ = () => {};
window.$RefreshSig$ = () => (type) => type;`;

  return {
    name: 'ensure-react-refresh-preamble',
    apply: 'serve',
    transformIndexHtml(html) {
      if (html.includes('injectIntoGlobalHook')) {
        return html;
      }

      return {
        html,
        tags: [
          {
            tag: 'script',
            attrs: { type: 'module' },
            children: preamble,
            injectTo: 'head-prepend',
          },
        ],
      };
    },
  };
}

export default defineConfig(({ command }) => ({
  // Keep serve mode in development even when Aspire sets NODE_ENV=production.
  mode: command === 'serve' ? 'development' : undefined,
  plugins: [react(), ensureReactRefreshPreamble()],
  server: {
    port,
    strictPort: underAspire,
    // Do not auto-open a browser under Aspire — Cursor/Aspire child processes often fail
    // ShellExecute (0x2). Open http://localhost:5173 from the Aspire dashboard or manually.
    open: false,
    proxy: {
      '/health': apiTarget,
      '/api': apiTarget,
    },
  },
}));
