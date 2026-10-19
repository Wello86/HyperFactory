export default function RegistrationModal({
  conferenceTitle,
  formData,
  loading,
  error,
  success,
  onChange,
  onSubmit,
  onClose,
}) {
  return (
    <div
      className="modal-overlay"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
    >
      <div className="modal">
        <div className="modal__header">
          <h2 className="modal__title">Register for {conferenceTitle}</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>

        {success ? (
          <div className="success-box">
            <h3>Registration Successful!</h3>
            <p>
              You&apos;ve been registered. A confirmation will be sent to{' '}
              <strong>{formData.email}</strong>.
            </p>
            <button className="btn btn--secondary" style={{ marginTop: '16px' }} onClick={onClose}>
              Close
            </button>
          </div>
        ) : (
          <form onSubmit={onSubmit}>
            {error && (
              <div className="error-container" style={{ marginBottom: '16px' }}>
                <p>{error}</p>
              </div>
            )}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0 16px' }}>
              <div className="form-group">
                <label>First Name *</label>
                <input
                  type="text"
                  name="firstName"
                  value={formData.firstName}
                  onChange={onChange}
                  required
                />
              </div>
              <div className="form-group">
                <label>Last Name *</label>
                <input
                  type="text"
                  name="lastName"
                  value={formData.lastName}
                  onChange={onChange}
                  required
                />
              </div>
            </div>
            <div className="form-group">
              <label>Email Address *</label>
              <input
                type="email"
                name="email"
                value={formData.email}
                onChange={onChange}
                required
              />
            </div>
            <div className="form-group">
              <label>Phone Number</label>
              <input type="tel" name="phone" value={formData.phone} onChange={onChange} />
            </div>
            <div className="form-group">
              <label>Company</label>
              <input type="text" name="company" value={formData.company} onChange={onChange} />
            </div>
            <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
              <button type="button" className="btn btn--ghost" onClick={onClose} disabled={loading}>
                Cancel
              </button>
              <button type="submit" className="btn btn--primary" disabled={loading}>
                {loading ? 'Registering...' : 'Complete Registration'}
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  )
}
