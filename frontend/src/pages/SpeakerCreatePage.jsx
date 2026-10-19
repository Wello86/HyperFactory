import { useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { createSpeaker } from '../api'

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

export default function SpeakerCreatePage() {
  const navigate = useNavigate()

  const [formData, setFormData] = useState({
    firstName: '',
    lastName: '',
    bio: '',
    email: '',
  })
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)

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
      setError(validationError)
      return
    }

    setLoading(true)
    setError(null)
    try {
      const created = await createSpeaker(formData)
      navigate(`/speakers/${created.id}`)
    } catch (err) {
      setError(err.message || 'Failed to create speaker')
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
          <Link to="/speakers">Speakers</Link>
          <span className="breadcrumb__sep">/</span>
          <span>New</span>
        </div>

        <h1 className="page-header__title">New Speaker</h1>

        <div className="card" style={{ maxWidth: '700px' }}>
          <form onSubmit={handleSubmit}>
            {error && (
              <div className="error-container" style={{ marginBottom: '16px' }}>
                <p>{error}</p>
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
              <Link to="/speakers" className="btn btn--ghost">
                Cancel
              </Link>
              <button type="submit" className="btn btn--primary" disabled={loading}>
                {loading ? 'Creating...' : 'Create Speaker'}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}
