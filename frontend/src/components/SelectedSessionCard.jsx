import { Link } from 'react-router-dom'

export default function SelectedSessionCard({ conferenceId, session, onDismiss }) {
  if (!session) return null

  return (
    <div className="detail-section">
      <h2 className="detail-section__title">Selected Session</h2>
      <div className="card" style={{ borderColor: '#6c9fff' }}>
        <div className="card__title">{session.title}</div>
        {session.description && <p className="card__description">{session.description}</p>}
        <div style={{ marginTop: '12px' }}>
          <Link
            to={`/conferences/${conferenceId}/sessions/${session.id}`}
            className="btn btn--secondary btn--sm"
          >
            View Details
          </Link>
          <button
            className="btn btn--ghost btn--sm"
            onClick={onDismiss}
            style={{ marginLeft: '8px' }}
          >
            Dismiss
          </button>
        </div>
      </div>
    </div>
  )
}
