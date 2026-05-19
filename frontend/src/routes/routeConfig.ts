export type RouteStatus = 'stub' | 'in-progress' | 'complete'

export interface RouteConfig {
  path: string
  label: string
  sourceView: string
  sourceViewModel: string
  status: RouteStatus
}

export const routes: RouteConfig[] = [
  {
    path: '/',
    label: 'Dashboard',
    sourceView: 'DashboardView',
    sourceViewModel: 'DashboardViewModel',
    status: 'in-progress',
  },
  {
    path: '/install',
    label: 'Install',
    sourceView: 'InstallView',
    sourceViewModel: 'InstallViewModel',
    status: 'stub',
  },
  {
    path: '/library',
    label: 'Library',
    sourceView: 'LibraryView',
    sourceViewModel: 'LibraryViewModel',
    status: 'stub',
  },
  {
    path: '/browse',
    label: 'Browse',
    sourceView: 'BrowseView',
    sourceViewModel: 'BrowseViewModel',
    status: 'stub',
  },
  {
    path: '/backups',
    label: 'Backups',
    sourceView: 'BackupsView',
    sourceViewModel: 'BackupsViewModel',
    status: 'stub',
  },
  {
    path: '/config',
    label: 'Config',
    sourceView: 'ConfigView',
    sourceViewModel: 'ConfigViewModel',
    status: 'stub',
  },
  {
    path: '/diagnostics',
    label: 'Diagnostics',
    sourceView: 'DiagnosticsView',
    sourceViewModel: 'DiagnosticsViewModel',
    status: 'stub',
  },
  {
    path: '/history',
    label: 'History',
    sourceView: 'HistoryView',
    sourceViewModel: 'HistoryViewModel',
    status: 'in-progress',
  },
  {
    path: '/profiles',
    label: 'Profiles',
    sourceView: 'ProfilesView',
    sourceViewModel: 'ProfilesViewModel',
    status: 'stub',
  },
  {
    path: '/settings',
    label: 'Settings',
    sourceView: 'SettingsView',
    sourceViewModel: 'SettingsViewModel',
    status: 'in-progress',
  },
  {
    path: '/logs',
    label: 'Log Viewer',
    sourceView: 'LogViewerView',
    sourceViewModel: 'LogViewerViewModel',
    status: 'in-progress',
  },
  {
    path: '/safe-mode',
    label: 'Safe Mode',
    sourceView: 'SafeModeView',
    sourceViewModel: 'SafeModeViewModel',
    status: 'stub',
  },
  {
    path: '/dev-diagnostics',
    label: 'Dev Diagnostics',
    sourceView: 'DevDiagnosticsView',
    sourceViewModel: 'DevDiagnosticsViewModel',
    status: 'stub',
  },
  {
    path: '/oiv',
    label: 'OIV Creator',
    sourceView: 'OivView',
    sourceViewModel: 'OivViewModel',
    status: 'stub',
  },
  {
    path: '/cleanup',
    label: 'Cleanup',
    sourceView: 'CleanupView',
    sourceViewModel: 'CleanupViewModel',
    status: 'stub',
  },
  {
    path: '/patrol-readiness',
    label: 'Patrol Readiness',
    sourceView: 'PatrolReadinessDashboardView',
    sourceViewModel: 'PatrolReadinessDashboardViewModel',
    status: 'stub',
  },
  {
    path: '/setup',
    label: 'Setup Wizard',
    sourceView: 'SetupWizardView',
    sourceViewModel: 'SetupWizardViewModel',
    status: 'stub',
  },
]
