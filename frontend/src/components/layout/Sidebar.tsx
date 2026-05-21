import { NavLink } from 'react-router-dom'
import { ChevronRight } from 'lucide-react'
import { routes, type RouteGroup } from '../../routes/routeConfig'

const NAV_ROUTES = routes.filter((r) => r.path !== '/setup')
const GROUPS: RouteGroup[] = ['Command', 'Operations', 'Tools', 'System']

export function Sidebar() {
  return (
    <nav
      aria-label="Main navigation"
      className="flex w-64 shrink-0 flex-col overflow-y-auto border-r border-[var(--color-border)] bg-[var(--color-surface)] px-3 py-4 max-[900px]:hidden"
    >
      <div className="mb-5 border-b border-[var(--color-border)] px-2 pb-4">
        <div className="text-base font-bold text-zinc-100">LSPDFR Manager</div>
        <div className="mt-1 text-xs text-zinc-500">Mod operations cockpit</div>
      </div>

      <div className="space-y-5">
        {GROUPS.map((group) => {
          const groupRoutes = NAV_ROUTES.filter((route) => route.group === group)
          if (groupRoutes.length === 0) return null

          return (
            <section key={group}>
              <div className="mb-2 px-2 text-[11px] font-bold uppercase tracking-wider text-zinc-600">
                {group}
              </div>
              <div className="space-y-1">
                {groupRoutes.map((route) => {
                  const Icon = route.icon
                  return (
                    <NavLink
                      key={route.path}
                      to={route.path}
                      end={route.path === '/'}
                      className={({ isActive }) =>
                        [
                          'group flex items-center gap-3 rounded-md border px-2.5 py-2 text-sm no-underline transition-colors',
                          isActive
                            ? 'border-[var(--color-border-strong)] bg-[var(--color-surface-raised)] text-zinc-100'
                            : 'border-transparent text-zinc-400 hover:bg-[var(--color-surface-raised)] hover:text-zinc-100',
                        ].join(' ')
                      }
                    >
                      {({ isActive }) => (
                        <>
                          <Icon
                            size={16}
                            className={isActive ? 'text-[var(--color-accent)]' : 'text-zinc-500'}
                          />
                          <span className="min-w-0 flex-1 truncate">{route.label}</span>
                          {route.status !== 'stub' && (
                            <span className="h-1.5 w-1.5 rounded-full bg-[var(--color-accent)]" />
                          )}
                          <ChevronRight
                            size={14}
                            className={isActive ? 'text-zinc-400' : 'text-transparent group-hover:text-zinc-600'}
                          />
                        </>
                      )}
                    </NavLink>
                  )
                })}
              </div>
            </section>
          )
        })}
      </div>
    </nav>
  )
}
