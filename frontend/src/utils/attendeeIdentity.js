const STORAGE_KEY = 'brownevents.attendeeEmail'

export function getStoredAttendeeEmail() {
  try {
    return localStorage.getItem(STORAGE_KEY)
  } catch {
    return null
  }
}

export function setStoredAttendeeEmail(email) {
  try {
    localStorage.setItem(STORAGE_KEY, email)
  } catch {
    // localStorage unavailable (e.g. private browsing) — registration still succeeds server-side
  }
}
