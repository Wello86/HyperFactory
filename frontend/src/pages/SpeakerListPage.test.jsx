import { render, screen, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { test, expect, vi } from 'vitest'
import SpeakerListPage from './SpeakerListPage'
import * as api from '../api'

vi.mock('../api')

test('renders an Add speaker link to /speakers/new', async () => {
  api.getSpeakers.mockResolvedValue([])
  render(
    <MemoryRouter>
      <SpeakerListPage />
    </MemoryRouter>
  )
  await waitFor(() =>
    expect(screen.getByRole('link', { name: /add speaker/i })).toHaveAttribute(
      'href',
      '/speakers/new'
    )
  )
})
