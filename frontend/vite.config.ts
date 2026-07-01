import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

// The SPA is built directly into the backend's wwwroot so the single ASP.NET image serves it.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../backend/src/Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      '/api': 'http://localhost:8080',
      '/healthz': 'http://localhost:8080',
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
  },
});
