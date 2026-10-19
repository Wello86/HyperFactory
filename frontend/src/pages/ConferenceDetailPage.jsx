import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import useConference from '../hooks/useConference'
import useConferenceSessions from '../hooks/useConferenceSessions'
import useConferenceRegistrations from '../hooks/useConferenceRegistrations'
import useRegistrationForm from '../hooks/useRegistrationForm'
import Navbar from '../components/Navbar'
import ConferenceHeader from '../components/ConferenceHeader'
import ConferenceInfoTable from '../components/ConferenceInfoTable'
import ConferenceSessionsSection from '../components/ConferenceSessionsSection'
import RegistrationsSection from '../components/RegistrationsSection'
import SelectedSessionCard from '../components/SelectedSessionCard'
import RegistrationModal from '../components/RegistrationModal'

const SESSIONS_PAGE_SIZE = 20

export default function ConferenceDetailPage() {
  const { id } = useParams()
  const [sessionsPage, setSessionsPage] = useState(1)

  const { conference, loading, error } = useConference(id)
  const sessionsState = useConferenceSessions(id, sessionsPage, SESSIONS_PAGE_SIZE)
  const registrationsState = useConferenceRegistrations(id)
  const registrationForm = useRegistrationForm(id)

  if (loading) {
    return (
      <div>
        <Navbar />
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
        <Navbar />
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
      <Navbar />

      <div className="page">
        <div className="breadcrumb">
          <Link to="/">Conferences</Link>
          <span className="breadcrumb__sep">/</span>
          <span>{conference?.title || 'Conference'}</span>
        </div>

        <ConferenceHeader conference={conference} conferenceId={id} />

        <div className="detail-section">
          <h2 className="detail-section__title">Details</h2>
          <ConferenceInfoTable conference={conference} />
        </div>

        <ConferenceSessionsSection
          conferenceId={id}
          sessions={sessionsState.sessions}
          sessionsPage={sessionsPage}
          sessionsPageSize={SESSIONS_PAGE_SIZE}
          sessionsTotalCount={sessionsState.totalCount}
          loading={sessionsState.loading}
          error={sessionsState.error}
          onPageChange={setSessionsPage}
          onSessionClick={registrationForm.setSelectedSession}
        />

        <RegistrationsSection
          registrations={registrationsState.registrations}
          loading={registrationsState.loading}
          error={registrationsState.error}
          status={conference?.status}
          onRegisterClick={registrationForm.open}
        />

        <SelectedSessionCard
          conferenceId={id}
          session={registrationForm.selectedSession}
          onDismiss={() => registrationForm.setSelectedSession(null)}
        />
      </div>

      {registrationForm.isOpen && (
        <RegistrationModal
          conferenceTitle={conference?.title}
          formData={registrationForm.formData}
          loading={registrationForm.loading}
          error={registrationForm.error}
          success={registrationForm.success}
          onChange={registrationForm.handleChange}
          onSubmit={registrationForm.handleSubmit}
          onClose={registrationForm.close}
        />
      )}
    </div>
  )
}
