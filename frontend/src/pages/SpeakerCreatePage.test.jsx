import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { test, expect, vi } from 'vitest'
import SpeakerCreatePage from './SpeakerCreatePage'
import * as api from '../api'

vi.mock('../api')
const navigateMock = vi.fn()
vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom')
  return { ...actual, useNavigate: () => navigateMock }
})

function renderPage() {
  render(
    <MemoryRouter>
      <SpeakerCreatePage />
    </MemoryRouter>
  )
}

test('submits valid form and navigates to the new speaker', async () => {
  api.createSpeaker.mockResolvedValue({ id: 7, firstName: 'Ada', lastName: 'Lovelace' })
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  await waitFor(() =>
    expect(api.createSpeaker).toHaveBeenCalledWith(
      expect.objectContaining({ firstName: 'Ada', lastName: 'Lovelace' })
    )
  )
  expect(navigateMock).toHaveBeenCalledWith('/speakers/7')
})

test('blocks submission and shows an error when first name is missing', () => {
  renderPage()
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  expect(screen.getByText(/first name is required/i)).toBeInTheDocument()
  expect(api.createSpeaker).not.toHaveBeenCalled()
})

test('blocks submission and shows an error for an invalid email', () => {
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'not-an-email' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  expect(screen.getByText(/valid email/i)).toBeInTheDocument()
  expect(api.createSpeaker).not.toHaveBeenCalled()
})

test('shows the API error message when creation fails', async () => {
  api.createSpeaker.mockRejectedValue(new Error('Boom'))
  renderPage()
  fireEvent.change(screen.getByLabelText(/first name/i), { target: { value: 'Ada' } })
  fireEvent.change(screen.getByLabelText(/last name/i), { target: { value: 'Lovelace' } })
  fireEvent.click(screen.getByRole('button', { name: /create speaker/i }))
  await waitFor(() => expect(screen.getByText('Boom')).toBeInTheDocument())
})
