import { Link } from 'react-router-dom'
import SessionList from './SessionList'
import Pagination from './Pagination'

export default function ConferenceSessionsSection({
  conferenceId,
  sessions,
  sessionsPage,
  sessionsPageSize,
  sessionsTotalCount,
  loading,
  error,
  onPageChange,
  onSessionClick,
}) {
  return (
    <div className="detail-section">
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
        }}
      >
        <h2 className="detail-section__title">Sessions</h2>
        <Link to={`/conferences/${conferenceId}/sessions/new`} className="btn btn--primary">
          Add Session
        </Link>
      </div>
      {loading ? (
        <div className="loading-container" style={{ padding: '40px 0' }}>
          <div className="spinner" />
          <p>Loading sessions...</p>
        </div>
      ) : error ? (
        <div className="error-container">
          <h3>Failed to load sessions</h3>
          <p>{error}</p>
        </div>
      ) : sessions.length === 0 ? (
        <div style={{ color: '#666', padding: '20px 0', textAlign: 'center' }}>
          No sessions scheduled yet.
        </div>
      ) : (
        <>
          <SessionList
            sessions={sessions}
            conferenceId={conferenceId}
            onSessionClick={onSessionClick}
          />
          <Pagination
            page={sessionsPage}
            pageSize={sessionsPageSize}
            totalCount={sessionsTotalCount}
            onPageChange={onPageChange}
          />
        </>
      )}
    </div>
  )
}
