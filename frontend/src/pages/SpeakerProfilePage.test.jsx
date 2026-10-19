import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { test, expect, vi } from 'vitest'
import SpeakerProfilePage from './SpeakerProfilePage'
import useSpeaker from '../hooks/useSpeaker'

vi.mock('../hooks/useSpeaker')

test('renders an Edit link to /speakers/:id/edit', () => {
  useSpeaker.mockReturnValue({
    speaker: { id: 7, firstName: 'Ada', lastName: 'Lovelace', sessions: [] },
    loading: false,
    error: null,
    notFound: false,
  })
  render(
    <MemoryRouter>
      <SpeakerProfilePage />
    </MemoryRouter>
  )
  expect(screen.getByRole('link', { name: /edit/i })).toHaveAttribute('href', '/speakers/7/edit')
})
