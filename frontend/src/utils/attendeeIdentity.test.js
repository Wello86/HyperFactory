import { describe, it, expect, beforeEach, vi } from 'vitest'
import { getStoredAttendeeEmail, setStoredAttendeeEmail } from './attendeeIdentity'

function createMemoryStorage() {
  const store = new Map()
  return {
    getItem: (key) => (store.has(key) ? store.get(key) : null),
    setItem: (key, value) => store.set(key, String(value)),
    removeItem: (key) => store.delete(key),
    clear: () => store.clear(),
  }
}

describe('attendeeIdentity', () => {
  beforeEach(() => {
    vi.stubGlobal('localStorage', createMemoryStorage())
  })

  it('returns null when nothing is stored', () => {
    expect(getStoredAttendeeEmail()).toBeNull()
  })

  it('round-trips a stored email', () => {
    setStoredAttendeeEmail('a@b.com')
    expect(getStoredAttendeeEmail()).toBe('a@b.com')
  })
})
