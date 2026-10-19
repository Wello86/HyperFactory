export function formatDate(dateString) {
  if (!dateString) return 'TBD'
  try {
    return new Date(dateString).toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
    })
  } catch {
    return dateString
  }
}

export function getStatusBadgeClass(status) {
  if (!status) return 'badge badge--upcoming'
  switch (status.toLowerCase()) {
    case 'active':
      return 'badge badge--active'
    case 'upcoming':
      return 'badge badge--upcoming'
    case 'cancelled':
      return 'badge badge--cancelled'
    case 'completed':
      return 'badge badge--completed'
    default:
      return 'badge badge--upcoming'
  }
}

export function isRegistrationOpen(status) {
  if (!status) return true
  return !['completed', 'cancelled'].includes(status.toLowerCase())
}
