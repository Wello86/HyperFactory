import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { test, expect, vi } from 'vitest'
import SpeakerEditPage from './SpeakerEditPage'
import * as api from '../api'

vi.mock('../api')
const navigateMock = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/speakers/7/edit']}>
      <Routes>
        <Route path="/speakers/:id/edit" element={<SpeakerEditPage />} />
      </Routes>
    </MemoryRouter>
  )
}

test('loads the speaker then submits an update and navigates back', async () => {
  api.getSpeaker.mockResolvedValue({
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    bio: '',
    email: '',
  })
  api.updateSpeaker.mockResolvedValue({ id: 7 })
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Hopper' } })
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  await waitFor(() =>
    expect(api.updateSpeaker).toHaveBeenCalledWith(
      '7',
      expect.objectContaining({ lastName: 'Hopper' })
    )
  )
  expect(navigateMock).toHaveBeenCalledWith('/speakers/7')
})

test('shows a load error when the speaker fetch fails', async () => {
  api.getSpeaker.mockRejectedValue(new Error('Not found'))
  renderPage()
  await waitFor(() => expect(screen.getByText('Not found')).toBeInTheDocument())
})

test('blocks submission and shows an error when last name is cleared', async () => {
  api.getSpeaker.mockResolvedValue({
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    bio: '',
    email: '',
  })
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: '' } })
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  expect(screen.getByText(/last name is required/i)).toBeInTheDocument()
  expect(api.updateSpeaker).not.toHaveBeenCalled()
})

test('blocks submission and shows an error for an invalid email', async () => {
  api.getSpeaker.mockResolvedValue({
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    bio: '',
    email: '',
  })
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'not-an-email' } })
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  expect(screen.getByText(/valid email/i)).toBeInTheDocument()
  expect(api.updateSpeaker).not.toHaveBeenCalled()
})

test('shows the API error message when the update fails', async () => {
  api.getSpeaker.mockResolvedValue({
    id: 7,
    firstName: 'Ada',
    lastName: 'Lovelace',
    bio: '',
    email: '',
  })
  api.updateSpeaker.mockRejectedValue(new Error('Boom'))
  renderPage()
  await waitFor(() => expect(screen.getByDisplayValue('Ada')).toBeInTheDocument())
  fireEvent.click(screen.getByRole('button', { name: /save changes/i }))
  await waitFor(() => expect(screen.getByText('Boom')).toBeInTheDocument())
})
