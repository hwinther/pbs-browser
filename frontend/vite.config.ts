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
    // Emit coverage/cobertura-coverage.xml so the reusable pr-build.yml node job
    // (irongut/CodeCoverageSummary) always finds it. coverage:ci passes --coverage.
    coverage: {
      reporter: ['cobertura', 'lcov', 'html', 'json'],
    },
    // Emit coverage/junit-report.xml so the reusable pr-build.yml node job
    // (dorny/test-reporter, jest-junit parser) always finds the report.
    reporters: ['verbose', 'github-actions', 'junit', 'json'],
    outputFile: {
      junit: './coverage/junit-report.xml',
      json: './coverage/json-report.json',
    },
  },
});
