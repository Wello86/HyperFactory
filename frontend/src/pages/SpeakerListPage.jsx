import { useState, useEffect } from 'react'
import { Link } from 'react-router-dom'
import { getSpeakers } from '../api'
import SpeakerCard from '../components/SpeakerCard'

export default function SpeakerListPage() {
  const [speakers, setSpeakers] = useState([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getSpeakers()
      .then((data) => {
        setSpeakers(Array.isArray(data) ? data : [])
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load speakers')
        setLoading(false)
      })
  }, [])

  return (
    <div>
      <nav className="navbar">
        <Link to="/" className="navbar__brand">
          BrownEvents
        </Link>
        <Link to="/" className="navbar__link">
          Conferences
        </Link>
        <Link to="/speakers" className="navbar__link">
          Speakers
        </Link>
      </nav>

      <div className="page">
        <div className="page-header">
          <h1 className="page-header__title">Speakers</h1>
          <Link to="/speakers/new" className="btn btn--primary">
            Add speaker
          </Link>
        </div>

        {loading && (
          <div className="loading-container">
            <div className="spinner" />
            <p>Loading speakers...</p>
          </div>
        )}

        {error && (
          <div className="error-container">
            <h3>Failed to load speakers</h3>
            <p>{error}</p>
          </div>
        )}

        {!loading && !error && speakers.length === 0 && (
          <div style={{ textAlign: 'center', color: '#666', padding: '40px 0' }}>
            No speakers found.
          </div>
        )}

        <div className="card-grid">
          {speakers.map((speaker) => (
            <SpeakerCard key={speaker.id} speaker={speaker} />
          ))}
        </div>
      </div>
    </div>
  )
}
