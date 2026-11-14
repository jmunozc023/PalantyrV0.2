import { defineConfig } from 'vite';
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
        target: 'http://127.0.0.1:5178',
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
