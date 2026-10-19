import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import SessionRegisterModal from '../components/SessionRegisterModal'
import useSessionRegistration from '../hooks/useSessionRegistration'

const EMPTY_FORM = { firstName: '', lastName: '', email: '' }

export default function SessionDetailPage() {
  const { id, sessionId } = useParams()
  const { session, loading, error, actionLoading, actionError, register, joinWaitlist, cancel } =
    useSessionRegistration(sessionId)
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [modalMode, setModalMode] = useState('register')
  const [formData, setFormData] = useState(EMPTY_FORM)

  function openModal(mode) {
    setFormData(EMPTY_FORM)
    setModalMode(mode)
    setIsModalOpen(true)
  }

  function closeModal() {
    setIsModalOpen(false)
  }

  function handleChange(e) {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
  }

  async function handleSubmit(e) {
    e.preventDefault()
    const success =
      modalMode === 'waitlist' ? await joinWaitlist(formData) : await register(formData)
    if (success) setIsModalOpen(false)
  }

  function formatTime(dt) {
    if (!dt) return '—'
    try {
      return new Date(dt).toLocaleTimeString('en-US', { hour: '2-digit', minute: '2-digit' })
    } catch {
      return dt
    }
  }

  function formatDate(dt) {
    if (!dt) return '—'
    try {
      return new Date(dt).toLocaleDateString('en-US', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
      })
    } catch {
      return dt
    }
  }

  if (loading)
    return (
      <div>
        <nav className="navbar">
          <Link to="/" className="navbar__brand">
            BrownEvents
          </Link>
        </nav>
        <div className="loading-container">
          <div className="spinner" />
          <p>Loading session...</p>
        </div>
      </div>
    )

  if (error)
    return (
      <div>
        <nav className="navbar">
          <Link to="/" className="navbar__brand">
            BrownEvents
          </Link>
        </nav>
        <div className="page">
          <div className="error-container">
            <h3>Failed to load session</h3>
            <p>{error}</p>
          </div>
        </div>
      </div>
    )

  const speaker = session?.speaker
  const room = session?.room

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
        <div className="breadcrumb">
          <Link to="/">Conferences</Link>
          <span className="breadcrumb__sep">/</span>
          <Link to={`/conferences/${id}`}>Conference</Link>
          <span className="breadcrumb__sep">/</span>
          <span>{session?.title}</span>
        </div>

        <div className="detail-header">
          <h1 className="detail-header__title">{session?.title}</h1>
          <div className="detail-header__meta">
            {session?.startTime && (
              <span>
                📅 {formatDate(session.startTime)} · {formatTime(session.startTime)} –{' '}
                {formatTime(session.endTime)}
              </span>
            )}
            {room && <span>🚪 {room.name}</span>}
            {typeof session?.capacity === 'number' && (
              <span>
                👥 {session.registeredCount ?? 0} / {session.capacity} registered
                {session.waitlistCount > 0 && ` · ${session.waitlistCount} waitlisted`}
              </span>
            )}
          </div>
          {session?.description && (
            <p className="detail-header__description">{session.description}</p>
          )}
        </div>

        {speaker && (
          <div className="detail-section">
            <h2 className="detail-section__title">Speaker</h2>
            <div className="card">
              <div className="card__title">
                {speaker.id ? (
                  <Link to={`/speakers/${speaker.id}`}>
                    {speaker.firstName} {speaker.lastName}
                  </Link>
                ) : (
                  <>
                    {speaker.firstName} {speaker.lastName}
                  </>
                )}
              </div>
              {speaker.bio && (
                <p className="card__description" style={{ WebkitLineClamp: 'unset' }}>
                  {speaker.bio}
                </p>
              )}
              {speaker.email && (
                <div style={{ fontSize: '0.85rem', color: '#888', marginTop: '8px' }}>
                  ✉ {speaker.email}
                </div>
              )}
            </div>
          </div>
        )}

        {room && (
          <div className="detail-section">
            <h2 className="detail-section__title">Room</h2>
            <div className="card">
              <div className="card__title">{room.name}</div>
              <div style={{ fontSize: '0.85rem', color: '#888', marginTop: '4px' }}>
                {room.location && <span>📍 {room.location}</span>}
                {room.capacity && (
                  <span style={{ marginLeft: '16px' }}>👥 Capacity: {room.capacity}</span>
                )}
              </div>
            </div>
          </div>
        )}

        {actionError && !isModalOpen && (
          <div className="error-container" style={{ marginTop: '16px' }}>
            <p>{actionError}</p>
          </div>
        )}

        <div style={{ marginTop: '24px' }}>
          <Link to={`/conferences/${id}`} className="btn btn--secondary">
            ← Back to Conference
          </Link>

          <Link
            to={`/conferences/${id}/sessions/${sessionId}/edit`}
            className="btn btn--secondary"
            style={{ marginLeft: '12px' }}
          >
            Edit
          </Link>

          {session?.currentAttendeeRegistrationId ? (
            <>
              <span className="badge badge--active" style={{ marginLeft: '12px' }}>
                You&apos;re registered
              </span>
              <button
                type="button"
                className="btn btn--secondary"
                style={{ marginLeft: '12px' }}
                onClick={cancel}
                disabled={actionLoading}
              >
                {actionLoading ? 'Cancelling...' : 'Cancel Registration'}
              </button>
            </>
          ) : session?.registeredCount >= session?.capacity ? (
            <button
              type="button"
              className="btn btn--primary"
              style={{ marginLeft: '12px' }}
              onClick={() => openModal('waitlist')}
              disabled={actionLoading}
            >
              Join Waitlist
            </button>
          ) : (
            <button
              type="button"
              className="btn btn--primary"
              style={{ marginLeft: '12px' }}
              onClick={() => openModal('register')}
              disabled={actionLoading}
            >
              Register for Session
            </button>
          )}
        </div>
      </div>

      {isModalOpen && (
        <SessionRegisterModal
          sessionTitle={session?.title}
          title={modalMode === 'waitlist' ? `Join Waitlist for ${session?.title}` : undefined}
          submitLabel={modalMode === 'waitlist' ? 'Join Waitlist' : 'Register'}
          submitLoadingLabel={modalMode === 'waitlist' ? 'Joining...' : 'Registering...'}
          formData={formData}
          loading={actionLoading}
          error={actionError}
          onChange={handleChange}
          onSubmit={handleSubmit}
          onClose={closeModal}
        />
      )}
    </div>
  )
}
