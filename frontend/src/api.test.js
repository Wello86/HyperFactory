import { test, expect, vi, afterEach } from 'vitest'
import { createSpeaker, updateSpeaker } from './api'

afterEach(() => {
  vi.unstubAllGlobals()
})

test('createSpeaker POSTs to /api/speakers and returns the parsed JSON', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 201,
    json: () => Promise.resolve({ id: 7, firstName: 'Ada' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  const result = await createSpeaker({ firstName: 'Ada' })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/speakers',
    expect.objectContaining({ method: 'POST', body: JSON.stringify({ firstName: 'Ada' }) })
  )
  expect(result).toEqual({ id: 7, firstName: 'Ada' })
})

test('updateSpeaker PUTs to /api/speakers/:id and returns the parsed JSON', async () => {
  const fetchMock = vi.fn().mockResolvedValue({
    ok: true,
    status: 200,
    json: () => Promise.resolve({ id: 7, firstName: 'Ada Hopper' }),
  })
  vi.stubGlobal('fetch', fetchMock)

  const result = await updateSpeaker(7, { firstName: 'Ada Hopper' })

  expect(fetchMock).toHaveBeenCalledWith(
    '/api/speakers/7',
    expect.objectContaining({ method: 'PUT', body: JSON.stringify({ firstName: 'Ada Hopper' }) })
  )
  expect(result).toEqual({ id: 7, firstName: 'Ada Hopper' })
})
