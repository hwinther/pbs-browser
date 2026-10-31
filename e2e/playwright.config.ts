/**
 * Drives an already-running pbs-browser in PBS_FAKE mode: the image from docker-compose.e2e.yml on
 * CI, or locally either that stack or `dotnet run` (Development picks the fake automatically when
 * the PBS client binary is absent; build the SPA first so wwwroot is populated).
 *
 * The specs assert on FakePbsClient's sample data (backend/src/Api/Pbs/FakePbsClient.cs).
 */
import { devices } from "@playwright/test";
import { definePlaywrightConfig } from "@wshno/playwright-config";

// Shared defaults (testDir ./tests, forbidOnly, retries, the github/html/json CI reporters at the
// paths the reusable reads, trace, screenshot and video on failure) come from @wshno/playwright-config.
export default definePlaywrightConfig({
  use: {
    baseURL: process.env.BASE_URL ?? "http://localhost:8080",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
