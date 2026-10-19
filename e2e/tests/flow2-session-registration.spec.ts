import { test, expect, type Page } from '@playwright/test'

async function openConferenceWithOpenSession(page: Page) {
  await page.goto('/')
  await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

  const cardCount = await page.locator('.card-grid .card').count()
  for (let i = 0; i < cardCount; i++) {
    await page.goto('/')
    await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
    await page.locator('.card-grid .card').nth(i).getByRole('link', { name: 'View Details' }).click()
    await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

    const sessionCount = await page.locator('.session-card').count()
    for (let j = 0; j < sessionCount; j++) {
      await page.locator('.session-card').nth(j).getByRole('link', { name: 'Details →' }).click()
      await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })

      const registerButton = page.getByRole('button', { name: 'Register for Session' })
      if (await registerButton.isVisible()) {
        return
      }

      await page.goBack()
      await expect(page.locator('.loading-container')).toBeHidden({ timeout: 15000 })
    }
  }

  throw new Error('No session with an open "Register for Session" button was found in the seeded stack')
}

test.describe.configure({ mode: 'serial' })

test('register for a session shows the registered badge', async ({ page }) => {
  await openConferenceWithOpenSession(page)

  await page.getByRole('button', { name: 'Register for Session' }).click()
  await expect(page.locator('.modal')).toBeVisible()

  const email = `Test.${Date.now()}@example.com`
  await page.locator('input[name="firstName"]').fill('Test')
  await page.locator('input[name="lastName"]').fill('Attendee')
  await page.locator('input[name="email"]').fill(email)
  await page.getByRole('button', { name: 'Register', exact: true }).click()

  await expect(page.locator('.modal')).toBeHidden({ timeout: 15000 })
  await expect(page.getByText("You're registered")).toBeVisible()
  await expect(page.getByRole('button', { name: 'Cancel Registration' })).toBeVisible()
})

test('submitting the registration form with an empty email is blocked by browser validation', async ({
  page,
}) => {
  await openConferenceWithOpenSession(page)

  await page.getByRole('button', { name: 'Register for Session' }).click()
  await expect(page.locator('.modal')).toBeVisible()

  await page.locator('input[name="firstName"]').fill('Test')
  await page.locator('input[name="lastName"]').fill('Attendee')
  await page.getByRole('button', { name: 'Register', exact: true }).click()

  const emailInput = page.locator('input[name="email"]')
  const isValid = await emailInput.evaluate((el: HTMLInputElement) => el.checkValidity())
  expect(isValid).toBe(false)

  await expect(page.locator('.modal')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Register', exact: true })).toBeVisible()
  await expect(page.locator('.badge--active')).toHaveCount(0)
})
