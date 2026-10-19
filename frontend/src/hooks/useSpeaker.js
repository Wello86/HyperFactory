import { useEffect, useRef, useState } from 'react'
import { getSpeaker } from '../api'

export default function useSpeaker(id) {
  const [speaker, setSpeaker] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [notFound, setNotFound] = useState(false)
  const lastIdRef = useRef(undefined)

  const isValidId = id !== undefined && id !== null && id !== '' && !Number.isNaN(Number(id))

  // Reset state during render (not in an effect) so a stale speaker/error
  // from a previous id can never paint under the new id, even for one commit.
  if (lastIdRef.current !== id) {
    lastIdRef.current = id
    setSpeaker(null)
    setError(null)
    setNotFound(false)
    setLoading(isValidId)
  }

  useEffect(() => {
    if (!isValidId) {
      setLoading(false)
      setError('Invalid speaker id')
      return undefined
    }

    let active = true
    setLoading(true)
    setError(null)
    setNotFound(false)

    getSpeaker(id)
      .then((data) => {
        if (!active) return
        setSpeaker(data ?? null)
        setLoading(false)
      })
      .catch((err) => {
        if (!active) return
        if (err.status === 404) {
          setNotFound(true)
          setError(err.detail || `No speaker exists with id ${id}.`)
        } else {
          setError(err.detail || err.message || 'Failed to load speaker')
        }
        setLoading(false)
      })

    return () => {
      active = false
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id])

  return { speaker, loading, error, notFound }
}
