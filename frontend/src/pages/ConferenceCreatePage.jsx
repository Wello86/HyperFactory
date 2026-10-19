import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { createConference } from '../api'

const STATUS_OPTIONS = ['UPCOMING', 'ACTIVE', 'CANCELLED', 'COMPLETED']

export default function ConferenceCreatePage() {
  const navigate = useNavigate()

  const [formData, setFormData] = useState({
    title: '',
    description: '',
    location: '',
    venue: '',
    startDate: '',
    endDate: '',
    status: 'UPCOMING',
    maxAttendees: '',
    organizerName: '',
    website: '',
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

  function handleChange(e) {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
  }

  function validate() {
    if (!formData.title.trim()) return 'Title is required.'
    if (!formData.startDate) return 'Start date is required.'
    if (!formData.endDate) return 'End date is required.'
    if (new Date(formData.endDate) < new Date(formData.startDate)) {
      return 'End date must be on or after the start date.'
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
      const created = await createConference({
        ...formData,
        maxAttendees: formData.maxAttendees ? Number(formData.maxAttendees) : null,
      })
      const conf = created.data || created
      navigate(`/conferences/${conf.id}`)
    } catch (err) {
      setError(err.message || 'Failed to create conference')
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
          <span>New</span>
        </div>

        <h1 className="page-header__title">New Conference</h1>

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
                <label>Location</label>
                <input
                  type="text"
                  name="location"
                  value={formData.location}
                  onChange={handleChange}
                />
              </div>
              <div className="form-group">
                <label>Venue</label>
                <input type="text" name="venue" value={formData.venue} onChange={handleChange} />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Start Date *</label>
                <input
                  type="date"
                  name="startDate"
                  value={formData.startDate}
                  onChange={handleChange}
                  required
                />
              </div>
              <div className="form-group">
                <label>End Date *</label>
                <input
                  type="date"
                  name="endDate"
                  value={formData.endDate}
                  onChange={handleChange}
                  required
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Status</label>
                <select name="status" value={formData.status} onChange={handleChange}>
                  {STATUS_OPTIONS.map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </div>
              <div className="form-group">
                <label>Max Attendees</label>
                <input
                  type="number"
                  name="maxAttendees"
                  min="0"
                  value={formData.maxAttendees}
                  onChange={handleChange}
                />
              </div>
            </div>

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>Organizer</label>
                <input
                  type="text"
                  name="organizerName"
                  value={formData.organizerName}
                  onChange={handleChange}
                />
              </div>
              <div className="form-group">
                <label>Website</label>
                <input type="url" name="website" value={formData.website} onChange={handleChange} />
              </div>
            </div>

            <div
              style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '8px' }}
            >
              <Link to="/" className="btn btn--ghost">
                Cancel
              </Link>
              <button type="submit" className="btn btn--primary" disabled={loading}>
                {loading ? 'Creating...' : 'Create Conference'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
