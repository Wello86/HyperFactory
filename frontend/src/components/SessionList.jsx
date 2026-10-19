import SessionCard from './SessionCard'

export default function SessionList({ sessions, conferenceId, onSessionClick }) {
  return (
    <div className="session-list">
      {sessions.map((session) => (
        <SessionCard
          key={session.id}
          session={session}
          conferenceId={conferenceId}
          onSessionClick={onSessionClick}
        />
      ))}
    </div>
  )
}
