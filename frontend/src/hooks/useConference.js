import { useEffect, useState } from 'react'
import { getConference } from '../api'

export default function useConference(id) {
  const [conference, setConference] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getConference(id)
      .then((data) => {
        setConference(data.data || data)
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load conference')
        setLoading(false)
      })
  }, [id])

  return { conference, loading, error }
}
