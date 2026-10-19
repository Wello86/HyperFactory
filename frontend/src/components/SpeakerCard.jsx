export default function SpeakerCard({ speaker }) {
  const name = `${speaker.firstName} ${speaker.lastName}`.trim()

  return (
    <div className="card">
      <div className="card__title">{name}</div>
      {speaker.bio && <p className="card__description">{speaker.bio}</p>}
      {speaker.email && (
        <div className="card__meta">
          <span>✉️ {speaker.email}</span>
        </div>
      )}
    </div>
  )
}
