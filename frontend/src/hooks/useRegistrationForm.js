import { useState } from 'react'
import { registerAttendee } from '../api'

const EMPTY_FORM = {
  firstName: '',
  lastName: '',
  email: '',
  phone: '',
  company: '',
  dietaryRequirements: '',
}

export default function useRegistrationForm(conferenceId) {
  const [isOpen, setIsOpen] = useState(false)
  const [selectedSession, setSelectedSession] = useState(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [success, setSuccess] = useState(false)
  const [formData, setFormData] = useState(EMPTY_FORM)

  function open() {
    setIsOpen(true)
    setSuccess(false)
    setError(null)
    setFormData(EMPTY_FORM)
  }

  function close() {
    setIsOpen(false)
    setSelectedSession(null)
    setError(null)
  }

  function handleChange(e) {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
  }

  async function handleSubmit(e) {
    e.preventDefault()
    setLoading(true)
    setError(null)
    try {
      await registerAttendee(conferenceId, {
        ...formData,
        sessionId: selectedSession ? selectedSession.id : null,
      })
      setSuccess(true)
      setLoading(false)
    } catch (err) {
      setError(err.message || 'Registration failed. Please try again.')
      setLoading(false)
    }
  }

  return {
    isOpen,
    selectedSession,
    setSelectedSession,
    loading,
    error,
    success,
    formData,
    open,
    close,
    handleChange,
    handleSubmit,
  }
}
