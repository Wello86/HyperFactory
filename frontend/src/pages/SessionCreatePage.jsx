import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { addSession, getSpeakers, getRooms } from '../api'

export default function SessionCreatePage() {
  const { id: conferenceId } = useParams()
  const navigate = useNavigate()

  const [formData, setFormData] = useState({
    title: '',
    description: '',
    startTime: '',
    endTime: '',
    capacity: '',
    speakerId: '',
    roomId: '',
  })
  const [speakers, setSpeakers] = useState([])
  const [rooms, setRooms] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  useEffect(() => {
    Promise.all([getSpeakers(), getRooms()])
      .then(([speakersData, roomsData]) => {
        setSpeakers(Array.isArray(speakersData) ? speakersData : [])
        setRooms(Array.isArray(roomsData) ? roomsData : [])
      })
      .catch((err) => {
        setError(err.message || 'Failed to load speakers/rooms')
      })
  }, [])

  function handleChange(e) {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
  }

  function validate() {
    if (!formData.title.trim()) return 'Title is required.'
    if (!formData.startTime) return 'Start time is required.'
    if (!formData.endTime) return 'End time is required.'
    if (new Date(formData.endTime) < new Date(formData.startTime)) {
      return 'End time must be on or after the start time.'
    }
    if (
      !formData.capacity ||
      !Number.isInteger(Number(formData.capacity)) ||
      Number(formData.capacity) <= 0
    ) {
      return 'Capacity is required and must be a positive integer.'
    }
    return null
  }

  async function handleSubmit(e) {
    e.preventDefault()
    const validationError = validate()
    if (validationError) {
      setError(validationError)
      return
    }

    setLoading(true)
    setError(null)
    try {
      await addSession(conferenceId, {
        ...formData,
        capacity: Number(formData.capacity),
        speakerId: formData.speakerId ? Number(formData.speakerId) : null,
        roomId: formData.roomId ? Number(formData.roomId) : null,
      })
      navigate(`/conferences/${conferenceId}`)
    } catch (err) {
      setError(err.message || 'Failed to create session')
      setLoading(false)
    }
  }

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
          <Link to={`/conferences/${conferenceId}`}>Conference</Link>
          <span className="breadcrumb__sep">/</span>
          <span>New Session</span>
        </div>

        <h1 className="page-header__title">New Session</h1>

        <div className="card" style={{ maxWidth: '700px' }}>
          <form onSubmit={handleSubmit}>
            {error && (
              <div className="error-container" style={{ marginBottom: '16px' }}>
                <p>{error}</p>
              </div>
            )}

            <div className="form-group">
              <label>Title *</label>
              <input
                type="text"
                name="title"
                value={formData.title}
                onChange={handleChange}
                required
              />
            </div>

            <div className="form-group">
              <label>Description</label>
              <textarea name="description" value={formData.description} onChange={handleChange} />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Start Time *</label>
                <input
                  type="datetime-local"
                  name="startTime"
                  value={formData.startTime}
                  onChange={handleChange}
                  required
                />
              </div>
              <div className="form-group">
                <label>End Time *</label>
                <input
                  type="datetime-local"
                  name="endTime"
                  value={formData.endTime}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Capacity *</label>
                <input
                  type="number"
                  name="capacity"
                  min="1"
                  value={formData.capacity}
                  onChange={handleChange}
                  required
                />
              </div>
              <div />
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Speaker</label>
                <select name="speakerId" value={formData.speakerId} onChange={handleChange}>
                  <option value="">— none —</option>
                  {speakers.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.firstName} {s.lastName}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label>Room</label>
                <select name="roomId" value={formData.roomId} onChange={handleChange}>
                  <option value="">— none —</option>
                  {rooms.map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.name}
                    </option>
                  ))}
                </select>
              </div>
            </div>

            <div
              style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '8px' }}
            >
              <Link to={`/conferences/${conferenceId}`} className="btn btn--ghost">
                Cancel
              </Link>
              <button type="submit" className="btn btn--primary" disabled={loading}>
                {loading ? 'Saving...' : 'Create Session'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
