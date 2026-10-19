import { useCallback, useEffect, useState } from 'react'
import {
  cancelSessionRegistration,
  getSession,
  joinWaitlist as joinWaitlistRequest,
  registerForSession,
} from '../api'
import { getStoredAttendeeEmail, setStoredAttendeeEmail } from '../utils/attendeeIdentity'

export default function useSessionRegistration(sessionId) {
  const [session, setSession] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [actionError, setActionError] = useState(null)

  const load = useCallback(() => {
    setLoading(true)
    setError(null)
    return getSession(sessionId, getStoredAttendeeEmail())
      .then((data) => {
        setSession(data.data || data)
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load session')
        setLoading(false)
      })
  }, [sessionId])

  useEffect(() => {
    load()
  }, [load])

  async function register(formData) {
    setActionLoading(true)
    setActionError(null)
    try {
      await registerForSession(sessionId, formData)
      setStoredAttendeeEmail(formData.email)
      await load()
      return true
    } catch (err) {
      setActionError(err.message || 'Registration failed. Please try again.')
      return false
    } finally {
      setActionLoading(false)
    }
  }

  async function joinWaitlist(formData) {
    setActionLoading(true)
    setActionError(null)
    try {
      await joinWaitlistRequest(sessionId, formData)
      setStoredAttendeeEmail(formData.email)
      await load()
      return true
    } catch (err) {
      setActionError(err.message || 'Joining the waitlist failed. Please try again.')
      return false
    } finally {
      setActionLoading(false)
    }
  }

  async function cancel() {
    setActionLoading(true)
    setActionError(null)
    try {
      await cancelSessionRegistration(sessionId, session.currentAttendeeRegistrationId)
      await load()
    } catch (err) {
      setActionError(err.message || 'Cancellation failed. Please try again.')
    } finally {
      setActionLoading(false)
    }
  }

  return { session, loading, error, actionLoading, actionError, register, joinWaitlist, cancel }
}
