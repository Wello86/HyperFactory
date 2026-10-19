import { useEffect, useState } from 'react'
import { getConferenceSessions } from '../api'

export default function useConferenceSessions(id, page, pageSize) {
  const [sessions, setSessions] = useState([])
  const [totalCount, setTotalCount] = useState(0)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getConferenceSessions(id, page, pageSize)
      .then((data) => {
        setSessions(Array.isArray(data.data) ? data.data : [])
        setTotalCount(data.totalCount || 0)
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load sessions')
        setLoading(false)
      })
  }, [id, page, pageSize])

  return { sessions, totalCount, loading, error }
}
