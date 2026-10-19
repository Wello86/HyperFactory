import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import useAttendeeDashboard from '../hooks/useAttendeeDashboard'
import { getStoredAttendeeEmail } from '../utils/attendeeIdentity'
import { formatDate, getStatusBadgeClass } from '../utils/format'
import Navbar from '../components/Navbar'

export default function DashboardPage() {
  const [emailInput, setEmailInput] = useState(getStoredAttendeeEmail() || '')
  const { registrations, loading, error, actionLoading, actionError, load, cancel } =
    useAttendeeDashboard()
  const [looked, setLooked] = useState(false)

  useEffect(() => {
    const storedEmail = getStoredAttendeeEmail()
    if (storedEmail) {
      setLooked(true)
      load(storedEmail)
    }
  }, [load])

  function handleSubmit(e) {
    e.preventDefault()
    if (!emailInput.trim()) return
    setLooked(true)
    load(emailInput.trim())
  }

  return (
    <div>
      <Navbar />

      <div className="page">
        <div className="detail-section">
          <h2 className="detail-section__title">My Registrations</h2>

          <form
            onSubmit={handleSubmit}
            style={{ display: 'flex', gap: '12px', marginBottom: '24px' }}
          >
            <input
              type="email"
              className="form-input"
              placeholder="you@example.com"
              value={emailInput}
              onChange={(e) => setEmailInput(e.target.value)}
              required
            />
            <button type="submit" className="btn btn--primary" disabled={loading}>
              {loading ? 'Looking up...' : 'Find My Registrations'}
            </button>
          </form>

          {actionError && (
            <div className="error-container" style={{ marginBottom: '16px' }}>
              <p>{actionError}</p>
            </div>
          )}

          {loading ? (
            <div className="loading-container" style={{ padding: '40px 0' }}>
              <div className="spinner" />
              <p>Loading registrations...</p>
            </div>
          ) : error ? (
            <div className="error-container">
              <h3>Failed to load registrations</h3>
              <p>{error}</p>
            </div>
          ) : !looked ? null : registrations.length === 0 ? (
            <div
              style={{ color: '#666', padding: '20px 0', textAlign: 'center', fontStyle: 'italic' }}
            >
              No registrations found for this email.
            </div>
          ) : (
            <div className="card">
              {registrations.map((reg) => (
                <div key={reg.id} className="attendee-row">
                  <span>
                    <Link to={`/conferences/${reg.conferenceId}`}>{reg.conferenceTitle}</Link>
                  </span>
                  <span>
                    {formatDate(reg.conferenceStartDate)} – {formatDate(reg.conferenceEndDate)}
                  </span>
                  <span>
                    <span className={getStatusBadgeClass(reg.status)}>{reg.status}</span>
                  </span>
                  <span>
                    {reg.status === 'CONFIRMED' && (
                      <button
                        className="btn btn--secondary"
                        disabled={actionLoading}
                        onClick={() => cancel(reg.conferenceId, reg.id)}
                      >
                        Cancel
                      </button>
                    )}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
