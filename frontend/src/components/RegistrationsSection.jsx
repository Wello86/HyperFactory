import { isRegistrationOpen } from '../utils/format'

export default function RegistrationsSection({
  registrations,
  loading,
  error,
  status,
  onRegisterClick,
}) {
  return (
    <div className="detail-section">
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: '20px',
        }}
      >
        <h2 className="detail-section__title" style={{ marginBottom: 0, borderBottom: 'none' }}>
          Registrations
        </h2>
        {isRegistrationOpen(status) && (
          <button className="btn btn--primary" onClick={onRegisterClick}>
            Register Now
          </button>
        )}
      </div>

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
      ) : registrations.length === 0 ? (
        <div style={{ color: '#666', padding: '20px 0', textAlign: 'center', fontStyle: 'italic' }}>
          No registrations yet.
        </div>
      ) : (
        <div className="card">
          {registrations.map((reg) => {
            const a = reg.attendee || {}
            return (
              <div key={reg.id} className="attendee-row">
                <span>
                  {a.firstName} {a.lastName}
                </span>
                <span>{a.email || '—'}</span>
                <span>
                  {reg.registeredAt ? new Date(reg.registeredAt).toLocaleDateString() : '—'}
                </span>
                <span>
                  <span className="badge badge--upcoming">{reg.status || 'CONFIRMED'}</span>
                </span>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
