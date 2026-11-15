// Vite is resolved at runtime by the build tool; allow type-checking when its
// package declarations are unavailable in the current workspace.
// @ts-expect-error -- vite is an optional build-time dependency in this context.
import { defineConfig } from 'vite';
// @ts-expect-error -- Vite's React plugin is an optional build-time dependency in this context.
import react from '@vitejs/plugin-react';

// NFR-PRIV-005: 100% local offline-first configuration.
// Proxies API traffic strictly to local Palantyr.Host loopback (127.0.0.1:5178).
export default defineConfig({
  plugins: [react()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5179',
        changeOrigin: false,
        secure: false,
      },
    },
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
});
