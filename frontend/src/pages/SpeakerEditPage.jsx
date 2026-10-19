import { useState, useEffect } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { getSpeaker, updateSpeaker } from '../api'

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export default function SpeakerEditPage() {
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
    getSpeaker(id)
      .then((data) => {
        const speaker = data.data || data
        setFormData({
          firstName: speaker.firstName || '',
          lastName: speaker.lastName || '',
          bio: speaker.bio || '',
          email: speaker.email || '',
        })
        setLoading(false)
      })
      .catch((err) => {
        setError(err.message || 'Failed to load speaker')
        setLoading(false)
      })
  }, [id])

  function handleChange(e) {
    const { name, value } = e.target
    setFormData((prev) => ({ ...prev, [name]: value }))
  }

  function validate() {
    if (!formData.firstName.trim()) return 'First name is required.'
    if (formData.firstName.length > 100) return 'First name must be 100 characters or fewer.'
    if (!formData.lastName.trim()) return 'Last name is required.'
    if (formData.lastName.length > 100) return 'Last name must be 100 characters or fewer.'
    if (formData.email && !EMAIL_REGEX.test(formData.email)) {
      return 'Please enter a valid email address.'
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
      await updateSpeaker(id, formData)
      navigate(`/speakers/${id}`)
    } catch (err) {
      setSubmitError(err.message || 'Failed to update speaker')
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
          <p>Loading speaker...</p>
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
            <h3>Failed to load speaker</h3>
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
          <Link to="/speakers">Speakers</Link>
          <span className="breadcrumb__sep">/</span>
          <Link to={`/speakers/${id}`}>{formData.firstName || 'Speaker'}</Link>
          <span className="breadcrumb__sep">/</span>
          <span>Edit</span>
        </div>

        <h1 className="page-header__title">Edit Speaker</h1>

        <div className="card" style={{ maxWidth: '700px' }}>
          <form onSubmit={handleSubmit}>
            {submitError && (
              <div className="error-container" style={{ marginBottom: '16px' }}>
                <p>{submitError}</p>
              </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label htmlFor="firstName">First Name *</label>
                <input
                  type="text"
                  id="firstName"
                  name="firstName"
                  value={formData.firstName}
                  onChange={handleChange}
                />
              </div>
              <div className="form-group">
                <label htmlFor="lastName">Last Name *</label>
                <input
                  type="text"
                  id="lastName"
                  name="lastName"
                  value={formData.lastName}
                  onChange={handleChange}
                />
              </div>
            </div>

            <div className="form-group">
              <label htmlFor="email">Email</label>
              <input
                type="text"
                id="email"
                name="email"
                value={formData.email}
                onChange={handleChange}
              />
            </div>

            <div className="form-group">
              <label htmlFor="bio">Bio</label>
              <textarea id="bio" name="bio" value={formData.bio} onChange={handleChange} />
            </div>

            <div
              style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end', marginTop: '8px' }}
            >
              <Link to={`/speakers/${id}`} className="btn btn--ghost">
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
