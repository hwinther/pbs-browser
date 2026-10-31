import { expect, test } from '@playwright/test'

// The input validation in front of the PBS client is the security boundary (see CLAUDE.md).
test.describe('api', () => {
  test('healthz answers', async ({ request }) => {
    const response = await request.get('/healthz')
    expect(response.status()).toBe(200)
    expect(await response.text()).toBe('ok')
  })

  test('rejects a snapshot with path traversal', async ({ request }) => {
    const response = await request.get('/api/catalog', {
      params: { snapshot: 'host/../../etc/2026-07-01T04:30:00Z' },
    })
    expect(response.status()).toBe(400)
  })

  test('rejects a restore path with traversal', async ({ request }) => {
    const response = await request.get('/api/download', {
      params: {
        snapshot: 'host/grafana/2026-07-01T04:30:00Z',
        archive: 'config.pxar',
        path: '/../etc/passwd',
      },
    })
    expect(response.status()).toBe(400)
  })

  test('serves the SPA for unknown routes', async ({ page }) => {
    await page.goto('/some/deep/link')
    await expect(page.getByRole('heading', { name: 'PBS Browser' })).toBeVisible()
  })
})
