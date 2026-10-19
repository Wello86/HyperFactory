import { test, expect } from '@playwright/test'

test('searching by a known keyword filters to the matching conference', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const firstCardTitle = await page.locator('.card-grid .card .card__title').first().innerText()
  const keyword = firstCardTitle.trim().split(/\s+/)[0]

  const searchInput = page.getByLabel('Search conferences')
  await searchInput.fill(keyword)

  await expect(async () => {
    const count = await page.locator('.card-grid .card').count()
    expect(count).toBeGreaterThan(0)
  }).toPass({ timeout: 15000 })

  await expect(
    page.locator('.card-grid .card .card__title', { hasText: keyword }).first()
  ).toBeVisible()
})

test('searching by a non-matching keyword shows the no-results message', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const searchInput = page.getByLabel('Search conferences')
  await searchInput.fill('zzz-no-such-conference-zzz')

  await expect(page.getByText('No conferences match your filters.')).toBeVisible({ timeout: 15000 })
  await expect(page.locator('.card-grid .card')).toHaveCount(0)
  await expect(page.locator('.error-container')).toHaveCount(0)
})
