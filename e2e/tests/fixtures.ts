/**
 * Every spec runs under the shared guards from @wshno/playwright-config: a CSP violation (Report-Only
 * included), a console error or warning, or a failed browser request fails the test. API-only checks
 * through `request` are not watched, so the 400s the validation specs expect are fine. coverageTest records
 * the page's V8 coverage for monocart-reporter when the CI config has it (playwright.config.ts).
 */
import { mergeTests } from '@playwright/test'
import { coverageTest, guardsTest } from '@wshno/playwright-config'

export { expect } from '@playwright/test'
export const test = mergeTests(guardsTest, coverageTest)
