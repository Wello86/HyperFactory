import { BrowserRouter, Routes, Route } from 'react-router-dom'
import ConferenceListPage from './pages/ConferenceListPage'
import ConferenceDetailPage from './pages/ConferenceDetailPage'
import ConferenceCreatePage from './pages/ConferenceCreatePage'
import ConferenceEditPage from './pages/ConferenceEditPage'
import SessionDetailPage from './pages/SessionDetailPage'
import SessionCreatePage from './pages/SessionCreatePage'
import SessionEditPage from './pages/SessionEditPage'
import RegistrationPage from './pages/RegistrationPage'
import SpeakerListPage from './pages/SpeakerListPage'
import SpeakerProfilePage from './pages/SpeakerProfilePage'
import SpeakerCreatePage from './pages/SpeakerCreatePage'
import SpeakerEditPage from './pages/SpeakerEditPage'
import DashboardPage from './pages/DashboardPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<ConferenceListPage />} />
        <Route path="/conferences/new" element={<ConferenceCreatePage />} />
        <Route path="/conferences/:id" element={<ConferenceDetailPage />} />
        <Route path="/conferences/:id/edit" element={<ConferenceEditPage />} />
        <Route path="/conferences/:id/sessions/new" element={<SessionCreatePage />} />
        <Route path="/conferences/:id/sessions/:sessionId" element={<SessionDetailPage />} />
        <Route path="/conferences/:id/sessions/:sessionId/edit" element={<SessionEditPage />} />
        <Route path="/conferences/:id/register" element={<RegistrationPage />} />
        <Route path="/speakers" element={<SpeakerListPage />} />
        <Route path="/speakers/new" element={<SpeakerCreatePage />} />
        <Route path="/speakers/:id" element={<SpeakerProfilePage />} />
        <Route path="/speakers/:id/edit" element={<SpeakerEditPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
      </Routes>
    </BrowserRouter>
  )
}
