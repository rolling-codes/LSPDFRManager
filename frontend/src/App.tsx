import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout'
import DashboardPage from './pages/DashboardPage'
import InstallPage from './pages/InstallPage'
import LibraryPage from './pages/LibraryPage'
import BrowsePage from './pages/BrowsePage'
import BackupsPage from './pages/BackupsPage'
import ConfigPage from './pages/ConfigPage'
import DiagnosticsPage from './pages/DiagnosticsPage'
import HistoryPage from './pages/HistoryPage'
import ProfilesPage from './pages/ProfilesPage'
import SettingsPage from './pages/SettingsPage'
import LogsPage from './pages/LogsPage'
import SafeModePage from './pages/SafeModePage'
import DevDiagnosticsPage from './pages/DevDiagnosticsPage'
import OivPage from './pages/OivPage'
import CleanupPage from './pages/CleanupPage'
import PatrolReadinessPage from './pages/PatrolReadinessPage'
import SetupWizardPage from './pages/SetupWizardPage'

const router = createBrowserRouter([
  {
    element: <AppLayout />,
    children: [
      { index: true, element: <DashboardPage /> },
      { path: 'install', element: <InstallPage /> },
      { path: 'library', element: <LibraryPage /> },
      { path: 'browse', element: <BrowsePage /> },
      { path: 'backups', element: <BackupsPage /> },
      { path: 'config', element: <ConfigPage /> },
      { path: 'diagnostics', element: <DiagnosticsPage /> },
      { path: 'history', element: <HistoryPage /> },
      { path: 'profiles', element: <ProfilesPage /> },
      { path: 'settings', element: <SettingsPage /> },
      { path: 'logs', element: <LogsPage /> },
      { path: 'safe-mode', element: <SafeModePage /> },
      { path: 'dev-diagnostics', element: <DevDiagnosticsPage /> },
      { path: 'oiv', element: <OivPage /> },
      { path: 'cleanup', element: <CleanupPage /> },
      { path: 'patrol-readiness', element: <PatrolReadinessPage /> },
      { path: 'setup', element: <SetupWizardPage /> },
    ],
  },
])

export default function App() {
  return <RouterProvider router={router} />
}
