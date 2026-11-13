/**
 * Drives an already-running pbs-browser in PBS_FAKE mode: the image from docker-compose.e2e.yml on
 * CI, or locally either that stack or `dotnet run` (Development picks the fake automatically when
 * the PBS client binary is absent; build the SPA first so wwwroot is populated).
 *
 * The specs assert on FakePbsClient's sample data (backend/src/Api/Pbs/FakePbsClient.cs).
 */
import { devices } from '@playwright/test'
import { ciReporters, definePlaywrightConfig, isCI, localReporters, monocartReporter } from '@wshno/playwright-config'

const baseURL = process.env.BASE_URL ?? 'http://localhost:8080'

// Shared defaults (testDir ./tests, forbidOnly, retries, the github/html/json CI reporters at the
// paths the reusable reads, trace, screenshot and video on failure) come from @wshno/playwright-config.
export default definePlaywrightConfig({
  // In CI, also monocart-reporter with the browser's V8 coverage of the SPA (recorded by coverageTest in
  // tests/fixtures.ts), mapped through the bundle's source maps to frontend/src/**, the same paths as
  // vitest's Cobertura: monocart-coverage/, which pr-e2e.yml uploads and the quality artifact counts.
  reporter: isCI
    ? [
        ...ciReporters,
        monocartReporter({
          name: 'pbs-browser E2E',
          coverage: {
            origin: baseURL,
            // The same files as the unit coverage (frontend/vite.config.ts): no entry point or test setup.
            layout: { root: 'frontend', sourceDirs: ['src'], exclude: /^frontend\/src\/(main\.tsx$|test\/)/ },
          },
        }),
      ]
    : localReporters,
  use: {
    baseURL,
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
