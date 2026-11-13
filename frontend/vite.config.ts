import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// The SPA is built directly into the backend's wwwroot so the single ASP.NET image serves it.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../backend/src/Api/wwwroot',
    emptyOutDir: true,
    // Source maps next to the bundle, so the e2e run's browser coverage maps back to src/. The repo is
    // public, so they disclose nothing new.
    sourcemap: true,
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
      // Every source file counts, not only the ones a test imports (Vitest's default), so an untested
      // file shows up as 0 %. Left out: the entry point and the test setup. The e2e coverage leaves out
      // the same (layout.exclude in e2e/playwright.config.ts).
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/main.tsx', 'src/test/**', 'src/**/*.test.{ts,tsx}', 'src/**/*.d.ts'],
    },
    // Emit coverage/junit-report.xml so the reusable pr-build.yml node job
    // (dorny/test-reporter, jest-junit parser) always finds the report.
    reporters: ['verbose', 'github-actions', 'junit', 'json'],
    outputFile: {
      junit: './coverage/junit-report.xml',
      json: './coverage/json-report.json',
    },
  },
})
