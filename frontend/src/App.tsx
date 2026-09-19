import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from './auth/AuthContext'
import { RequireAuth, RequireRole } from './auth/RoleGuard'
import AppShell from './components/AppShell'
import LoginPage from './pages/LoginPage'
import AcceptInvitePage from './pages/AcceptInvitePage'
import SpeakersListPage from './pages/collector/SpeakersListPage'
import SpeakerSessionPage from './pages/collector/SpeakerSessionPage'
import RecordingCapturePage from './pages/collector/RecordingCapturePage'
import HistoryPage from './pages/collector/HistoryPage'
import AnnotationQueuePage from './pages/annotator/AnnotationQueuePage'
import AllAnnotationsPage from './pages/annotator/AllAnnotationsPage'
import UsersPage from './pages/admin/UsersPage'
import RecordingsBrowserPage from './pages/admin/RecordingsBrowserPage'
import ExportPage from './pages/admin/ExportPage'
import DashboardPage from './pages/DashboardPage'

function HomeRedirect() {
  const { user } = useAuth()
  if (!user) return <Navigate to="/login" replace />
  if (user.roles.includes('Admin')) return <Navigate to="/dashboard" replace />
  // A multi-role (Collector + Annotator) user lands on their primary field task;
  // the nav bar carries the union of both roles' menus so they can reach the rest.
  if (user.roles.includes('Collector')) return <Navigate to="/collector/speakers" replace />
  if (user.roles.includes('Annotator')) return <Navigate to="/annotator/queue" replace />
  return <Navigate to="/dashboard" replace />
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/invite" element={<AcceptInvitePage />} />

      <Route element={<RequireAuth />}>
        <Route element={<AppShell />}>
          <Route path="/" element={<HomeRedirect />} />
          <Route path="/dashboard" element={<DashboardPage />} />

          <Route element={<RequireRole roles={['Collector']} />}>
            <Route path="/collector/speakers" element={<SpeakersListPage />} />
            <Route
              path="/collector/speakers/:speakerId/sessions/:sessionId"
              element={<SpeakerSessionPage />}
            />
            <Route
              path="/collector/speakers/:speakerId/sessions/:sessionId/record"
              element={<RecordingCapturePage />}
            />
            <Route path="/collector/history" element={<HistoryPage />} />
          </Route>

          <Route element={<RequireRole roles={['Annotator', 'Admin']} />}>
            <Route path="/annotator/queue" element={<AnnotationQueuePage />} />
            <Route path="/annotator/all" element={<AllAnnotationsPage />} />
          </Route>

          <Route element={<RequireRole roles={['Admin']} />}>
            <Route path="/admin/users" element={<UsersPage />} />
            <Route path="/admin/recordings" element={<RecordingsBrowserPage />} />
            <Route path="/admin/export" element={<ExportPage />} />
          </Route>
        </Route>
      </Route>

      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}
