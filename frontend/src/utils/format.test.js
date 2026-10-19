import { describe, it, expect } from 'vitest'
import { formatDate, getStatusBadgeClass, isRegistrationOpen } from './format'

describe('formatDate', () => {
  it('returns TBD for falsy input', () => {
    expect(formatDate(null)).toBe('TBD')
  })
  it('formats a valid date string', () => {
    expect(formatDate('2026-01-15')).toBe('January 15, 2026')
  })
})

describe('getStatusBadgeClass', () => {
  it('maps active to the active badge class', () => {
    expect(getStatusBadgeClass('active')).toBe('badge badge--active')
  })
  it('defaults unknown statuses to upcoming', () => {
    expect(getStatusBadgeClass('bogus')).toBe('badge badge--upcoming')
  })
})

describe('isRegistrationOpen', () => {
  it('is false for completed events', () => {
    expect(isRegistrationOpen('completed')).toBe(false)
  })
  it('is true when status is missing', () => {
    expect(isRegistrationOpen(undefined)).toBe(true)
  })
})
