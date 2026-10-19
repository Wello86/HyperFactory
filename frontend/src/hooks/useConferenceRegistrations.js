import { useEffect, useState } from 'react'
import { getConferenceRegistrations } from '../api'

export default function useConferenceRegistrations(id) {
  const [registrations, setRegistrations] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getConferenceRegistrations(id)
      .then((data) => {
        setRegistrations(Array.isArray(data) ? data : [])
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load registrations')
        setLoading(false)
      })
  }, [id])

  return { registrations, loading, error }
}
