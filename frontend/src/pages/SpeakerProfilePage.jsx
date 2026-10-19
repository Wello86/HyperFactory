import { useParams, Link } from 'react-router-dom'
import useSpeaker from '../hooks/useSpeaker'
import Navbar from '../components/Navbar'

function formatDate(dt) {
  if (!dt) return '—'
  const date = new Date(dt)
  if (Number.isNaN(date.getTime())) return dt
  return date.toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'long',
    day: 'numeric',
  })
}

function formatTime(dt) {
  if (!dt) return '—'
  const date = new Date(dt)
  if (Number.isNaN(date.getTime())) return dt
  return date.toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
}

export default function SpeakerProfilePage() {
  const { id } = useParams()
  const { speaker, loading, error, notFound } = useSpeaker(id)

  if (loading) {
    return (
      <div>
        <Navbar />
        <div className="loading-container">
          <div className="spinner" />
          <p>Loading speaker...</p>
        </div>
      </div>
    )
  }

  if (notFound) {
    return (
      <div>
        <Navbar />
        <div className="page">
          <div className="error-container">
            <h3>Speaker not found</h3>
            <p>{error}</p>
          </div>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div>
        <Navbar />
        <div className="page">
          <div className="error-container">
            <h3>Failed to load speaker</h3>
            <p>{error}</p>
          </div>
        </div>
      </div>
    )
  }

  if (!speaker) {
    return (
      <div>
        <Navbar />
        <div className="page">
          <div className="error-container">
            <h3>Speaker not found</h3>
          </div>
        </div>
      </div>
    )
  }

  const sessions = speaker?.sessions || []

  return (
    <div>
      <Navbar />

      <div className="page">
        <div className="breadcrumb">
          <Link to="/speakers">Speakers</Link>
          <span className="breadcrumb__sep">/</span>
          <span>
            {speaker?.firstName} {speaker?.lastName}
          </span>
        </div>

        <div className="detail-header">
          <h1 className="detail-header__title">
            {speaker?.firstName} {speaker?.lastName}
          </h1>
          {speaker?.email && (
            <div className="detail-header__meta">
              <span>✉ {speaker.email}</span>
            </div>
          )}
          {speaker?.bio && <p className="detail-header__description">{speaker.bio}</p>}
          <Link to={`/speakers/${speaker?.id}/edit`} className="btn btn--ghost">
            Edit
          </Link>
        </div>

        <div className="detail-section">
          <h2 className="detail-section__title">Sessions</h2>
          {sessions.length === 0 ? (
            <p>This speaker has no sessions yet.</p>
          ) : (
            sessions.map((session) => (
              <div className="card" key={session.sessionId}>
                <div className="card__title">{session.title}</div>
                <div style={{ fontSize: '0.85rem', color: '#888', marginTop: '4px' }}>
                  <span>🎪 {session.conferenceTitle}</span>
                  <span style={{ marginLeft: '16px' }}>
                    📅 {formatDate(session.startTime)} · {formatTime(session.startTime)} –{' '}
                    {formatTime(session.endTime)}
                  </span>
                </div>
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  )
}
