import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { getConference, updateConference } from '../api'

const STATUS_OPTIONS = ['UPCOMING', 'ACTIVE', 'CANCELLED', 'COMPLETED']

function toDateInputValue(dateString) {
  if (!dateString) return ''
  return dateString.slice(0, 10)
}

export default function ConferenceEditPage() {
  const { id } = useParams()
  const navigate = useNavigate()

  const [formData, setFormData] = useState(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState(null)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState(null)

  useEffect(() => {
    setLoading(true)
    setError(null)
    getConference(id)
      .then((data) => {
        const conf = data.data || data
        setFormData({
          title: conf.title || '',
          description: conf.description || '',
          location: conf.location || '',
          venue: conf.venue || '',
          startDate: toDateInputValue(conf.startDate),
          endDate: toDateInputValue(conf.endDate),
          status: conf.status || 'UPCOMING',
          maxAttendees: conf.maxAttendees ?? '',
          organizerName: conf.organizerName || '',
          website: conf.website || '',
        })
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load conference')
        setLoading(false)
      })
  }, [id])

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
      setSubmitError(validationError)
      return
    }

    setSubmitting(true)
    setSubmitError(null)
    try {
      await updateConference(id, {
        ...formData,
        maxAttendees: formData.maxAttendees ? Number(formData.maxAttendees) : null,
      })
      navigate(`/conferences/${id}`)
    } catch (err) {
      setSubmitError(err.message || 'Failed to update conference')
      setSubmitting(false)
    }
  }

  const nav = (
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
  )

  if (loading) {
    return (
      <div>
        {nav}
        <div className="loading-container">
          <div className="spinner" />
          <p>Loading conference...</p>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div>
        {nav}
        <div className="page">
          <div className="error-container">
            <h3>Failed to load conference</h3>
            <p>{error}</p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <div>
      {nav}

      <div className="page">
        <div className="breadcrumb">
          <Link to="/">Conferences</Link>
          <span className="breadcrumb__sep">/</span>
          <Link to={`/conferences/${id}`}>{formData.title || 'Conference'}</Link>
          <span className="breadcrumb__sep">/</span>
          <span>Edit</span>
        </div>

        <h1 className="page-header__title">Edit Conference</h1>

        <div className="card" style={{ maxWidth: '700px' }}>
          <form onSubmit={handleSubmit}>
            {submitError && (
              <div className="error-container" style={{ marginBottom: '16px' }}>
                <p>{submitError}</p>
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
              <Link to={`/conferences/${id}`} className="btn btn--ghost">
                Cancel
              </Link>
              <button type="submit" className="btn btn--primary" disabled={submitting}>
                {submitting ? 'Saving...' : 'Save Changes'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
