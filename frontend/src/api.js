const BASE_URL = ''

async function request(path, options = {}) {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json', ...options.headers },
    ...options,
  })
  if (!res.ok) {
    const text = await res.text()
    let detail
    try {
      detail = text ? JSON.parse(text).detail : undefined
    } catch {
      detail = undefined
    }
    const error = new Error(detail || text || `HTTP ${res.status}`)
    error.status = res.status
    error.detail = detail
    throw error
  }
  if (res.status === 204) return null
  return res.json()
}

export const getConferences = (page = 1, pageSize = 20, filters = {}) => {
  const { search, from, to, status } = filters
  const params = new URLSearchParams({ page, pageSize })
  if (search) params.set('search', search)
  if (from) params.set('from', from)
  if (to) params.set('to', to)
  if (status) params.set('status', status)
  return request(`/api/conferences?${params.toString()}`)
}

export const getConference = (id) => request(`/api/conferences/${id}`)

export const createConference = (data) =>
  request('/api/conferences', { method: 'POST', body: JSON.stringify(data) })

export const updateConference = (id, data) =>
  request(`/api/conferences/${id}`, { method: 'PUT', body: JSON.stringify(data) })

export const getConferenceSessions = (id, page = 1, pageSize = 20) =>
  request(`/api/conferences/${id}/sessions?page=${page}&pageSize=${pageSize}`)

export const getConferenceRegistrations = (id) => request(`/api/conferences/${id}/registrations`)

export const registerAttendee = (id, data) =>
  request(`/api/conferences/${id}/register`, { method: 'POST', body: JSON.stringify(data) })

export const deleteRegistration = (conferenceId, registrationId) =>
  request(`/api/conferences/${conferenceId}/registrations/${registrationId}`, { method: 'DELETE' })

export const getSession = (id, email) =>
  request(`/api/sessions/${id}${email ? `?email=${encodeURIComponent(email)}` : ''}`)

export const registerForSession = (id, data) =>
  request(`/api/sessions/${id}/register`, { method: 'POST', body: JSON.stringify(data) })

export const joinWaitlist = (sessionId, data) =>
  request(`/api/sessions/${sessionId}/waitlist`, { method: 'POST', body: JSON.stringify(data) })

export const cancelSessionRegistration = (sessionId, registrationId) =>
  request(`/api/sessions/${sessionId}/registrations/${registrationId}`, { method: 'DELETE' })

export const getAttendeeRegistrations = (email) =>
  request(`/api/attendees/${encodeURIComponent(email)}/registrations`)

export const getSpeakers = () => request('/api/speakers')

export const getSpeaker = (id) => request(`/api/speakers/${id}`)

export const getRooms = () => request('/api/rooms')

export const addSession = (conferenceId, data) =>
  request(`/api/conferences/${conferenceId}/sessions`, {
    method: 'POST',
    body: JSON.stringify(data),
  })

export const updateSession = (id, data) =>
  request(`/api/sessions/${id}`, { method: 'PUT', body: JSON.stringify(data) })

export const createSpeaker = (data) =>
  request('/api/speakers', { method: 'POST', body: JSON.stringify(data) })

export const updateSpeaker = (id, data) =>
  request(`/api/speakers/${id}`, { method: 'PUT', body: JSON.stringify(data) })
