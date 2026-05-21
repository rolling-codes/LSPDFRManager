import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { CircleDot, Shield } from 'lucide-react'
import { routes } from '../../routes/routeConfig'
import { Sidebar } from './Sidebar'
import { StatusBadge } from '../ui/Page'

export function AppLayout() {
  const location = useLocation()
  const navigate = useNavigate()
  const activeRoute =
    routes.find((route) => route.path !== '/' && location.pathname.startsWith(route.path)) ??
    routes.find((route) => route.path === '/')

  return (
    <div className="app-shell">
      <Sidebar />
      <div className="app-main">
        <header className="app-topbar">
          <div className="min-w-0">
            <div className="flex items-center gap-2 text-xs font-semibold uppercase tracking-wider text-zinc-500">
              <Shield size={14} />
              Desktop command center
            </div>
            <div className="mt-1 truncate text-sm text-zinc-300">
              {activeRoute?.description ?? 'Manage LSPDFR safely and predictably'}
            </div>
          </div>
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <StatusBadge tone="success">
              <CircleDot size={12} />
              Local only
            </StatusBadge>
            <StatusBadge tone="neutral">React Preview</StatusBadge>
          </div>
          <select
            className="input hidden w-full max-[900px]:block"
            aria-label="Navigate"
            value={activeRoute?.path ?? '/'}
            onChange={(event) => navigate(event.target.value)}
          >
            {routes
              .filter((route) => route.path !== '/setup')
              .map((route) => (
                <option key={route.path} value={route.path}>
                  {route.label}
                </option>
              ))}
          </select>
        </header>
        <main className="app-content">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
