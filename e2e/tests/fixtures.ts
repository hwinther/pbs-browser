/**
 * Every spec runs under the shared guards from @wshno/playwright-config: a CSP violation (Report-Only
 * included), a console error or warning, or a failed browser request fails the test. API-only checks
 * through `request` are not watched, so the 400s the validation specs expect are fine.
 */
export { expect } from '@playwright/test'
export { guardsTest as test } from '@wshno/playwright-config'
