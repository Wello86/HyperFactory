export default function SessionRegisterModal({
  sessionTitle,
  title,
  submitLabel = 'Register',
  submitLoadingLabel = 'Registering...',
  formData,
  loading,
  error,
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
          <h2 className="modal__title">{title || `Register for ${sessionTitle}`}</h2>
          <button className="modal__close" onClick={onClose}>
            ✕
          </button>
        </div>

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
            <input type="email" name="email" value={formData.email} onChange={onChange} required />
          </div>
          <div style={{ display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
            <button type="button" className="btn btn--ghost" onClick={onClose} disabled={loading}>
              Cancel
            </button>
            <button type="submit" className="btn btn--primary" disabled={loading}>
              {loading ? submitLoadingLabel : submitLabel}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
