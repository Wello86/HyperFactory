import { Link } from 'react-router-dom'
import { formatDate, getStatusBadgeClass } from '../utils/format'

export default function ConferenceHeader({ conference, conferenceId }) {
  return (
    <div className="detail-header">
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          gap: '12px',
          marginBottom: '12px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <h1 className="detail-header__title" style={{ marginBottom: 0 }}>
            {conference?.title}
          </h1>
          <span className={getStatusBadgeClass(conference?.status)}>
            {conference?.status || 'Upcoming'}
          </span>
        </div>
        <Link to={`/conferences/${conferenceId}/edit`} className="btn btn--secondary">
          Edit
        </Link>
      </div>
      <div className="detail-header__meta">
        {conference?.location && <span>📍 {conference.location}</span>}
        {conference?.startDate && (
          <span>
            📅 {formatDate(conference.startDate)} — {formatDate(conference.endDate)}
          </span>
        )}
      </div>
      {conference?.description && (
        <p className="detail-header__description">{conference.description}</p>
      )}
    </div>
  )
}
