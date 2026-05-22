import { useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { fetchConfig } from '../../lib/api/config'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { CircleDot, Shield } from 'lucide-react'
import { routes } from '../../routes/routeConfig'
import { Sidebar } from './Sidebar'
import { StatusBadge } from '../ui/Page'

export function AppLayout() {
  const location = useLocation()
  const navigate = useNavigate()

  const { data: config, isLoading } = useQuery({
    queryKey: ['config'],
    queryFn: fetchConfig,
  })

  useEffect(() => {
    if (!isLoading && config && (!config.gtaPath || !config.gtaPathValid) && location.pathname !== '/setup') {
      navigate('/setup')
    }
  }, [config, isLoading, location.pathname, navigate])

  const activeRoute =
    routes.find((route) => route.path !== '/' && location.pathname.startsWith(route.path)) ??
    routes.find((route) => route.path === '/')

  if (isLoading) {
    return (
      <div className="flex h-screen items-center justify-center bg-zinc-950 text-zinc-400">
        <div className="flex flex-col items-center gap-3">
          <div className="h-8 w-8 animate-spin rounded-full border-2 border-zinc-700 border-t-[var(--color-accent,indigo-500)]" />
          <span className="text-sm font-medium">Checking configuration...</span>
        </div>
      </div>
    )
  }

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
