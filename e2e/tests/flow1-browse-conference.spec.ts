import { test, expect } from '@playwright/test'

test('browse conferences -> detail -> sessions', async ({ page }) => {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
  const firstCard = page.locator('.card-grid .card').first()
  await expect(firstCard).toBeVisible()

  await firstCard.getByRole('link', { name: 'View Details' }).click()
  await expect(page).toHaveURL(/\/conferences\/[^/]+$/)
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
  await expect(page.getByRole('link', { name: 'Conferences' }).first()).toBeVisible()

  const sessionsHeading = page.locator('h2.detail-section__title', { hasText: 'Sessions' })
  await expect(sessionsHeading).toBeVisible()
})

test('detail page shows error state for a non-existent conference id', async ({ page }) => {
  await page.goto('/conferences/00000000-0000-0000-0000-000000000000')
  const errorContainer = page.locator('.error-container')
  await expect(errorContainer).toBeVisible({ timeout: 15000 })
  await expect(errorContainer.getByRole('heading', { name: 'Failed to load conference' })).toBeVisible()
})
