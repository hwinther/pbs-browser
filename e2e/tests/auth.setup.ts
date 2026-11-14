/**
 * Signs the e2e user in on a deployed env (an https BASE_URL, the nightly preview), once per run.
 *
 * Only playwright.config.ts with such a BASE_URL runs this (its `setup` project); the compose run
 * talks to the app directly. The app has no sign-in of its own: Authelia forward-auth on the host is
 * the gate, so the first navigation lands on auth.wsh.no for the password and TOTP and then comes back.
 * The saved storage state (Authelia's session cookie) is what the chromium project and its `request`
 * fixture start from.
 */

import { expect, test as setup } from '@playwright/test'
import * as OTPAuth from 'otpauth'

import { authFile } from './auth-paths'

function requiredEnv(name: string): string {
  const value = process.env[name]
  if (!value) throw new Error(`${name} must be set to sign in on a deployed env`)
  return value
}

setup('authenticate', async ({ page, baseURL }) => {
  const username = requiredEnv('E2E_USERNAME')
  const password = requiredEnv('E2E_PASSWORD')
  const otpSecret = requiredEnv('E2E_OTP_SECRET')
  const appHost = new URL(baseURL ?? '').hostname

  await page.goto('/')
  await page.waitForURL(/auth\.wsh\.no/, { timeout: 15_000 })
  await page.getByRole('textbox', { name: /username/i }).fill(username)
  await page.locator('input[type="password"]').fill(password)
  await page.getByRole('button', { name: 'Sign in' }).click()
  await page.waitForURL(/auth\.wsh\.no\/2fa/, { timeout: 10_000 })

  // Generated just before typing, so a slow login can't leave a stale code. Authelia renders one input
  // per digit inside #otp-input; type into the first and it advances and submits by itself.
  const code = new OTPAuth.TOTP({ secret: otpSecret, digits: 6, period: 30 }).generate()
  const otpInputs = page.locator('#otp-input').getByRole('textbox')
  await expect(otpInputs).toHaveCount(code.length)
  await otpInputs.first().click()
  await page.keyboard.type(code)
  await page.waitForURL((url) => url.hostname === appHost, { timeout: 15_000 })
  await expect(page.getByRole('heading', { name: 'PBS Browser' })).toBeVisible()

  await page.context().storageState({ path: authFile })
})
