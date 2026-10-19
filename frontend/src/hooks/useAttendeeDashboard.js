import { useCallback, useState } from 'react'
import { deleteRegistration, getAttendeeRegistrations } from '../api'
import { setStoredAttendeeEmail } from '../utils/attendeeIdentity'

export default function useAttendeeDashboard() {
  const [email, setEmail] = useState(null)
  const [registrations, setRegistrations] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [actionLoading, setActionLoading] = useState(false)
  const [actionError, setActionError] = useState(null)

  const load = useCallback(async (lookupEmail) => {
    setLoading(true)
    setError(null)
    try {
      const data = await getAttendeeRegistrations(lookupEmail)
      setRegistrations(data)
      setEmail(lookupEmail)
      setStoredAttendeeEmail(lookupEmail)
    } catch (err) {
      if (err.status === 404) {
        setRegistrations([])
        setEmail(lookupEmail)
        setStoredAttendeeEmail(lookupEmail)
      } else {
        setError(err.message || 'Failed to load registrations')
      }
    } finally {
      setLoading(false)
    }
  }, [])

  async function cancel(conferenceId, registrationId) {
    setActionLoading(true)
    setActionError(null)
    try {
      await deleteRegistration(conferenceId, registrationId)
      await load(email)
    } catch (err) {
      setActionError(err.message || 'Cancellation failed. Please try again.')
    } finally {
      setActionLoading(false)
    }
  }

  return { registrations, loading, error, actionLoading, actionError, load, cancel }
}
