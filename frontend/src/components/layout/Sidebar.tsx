import { NavLink } from 'react-router-dom'
import { routes } from '../../routes/routeConfig'

const NAV_ROUTES = routes.filter((r) => r.path !== '/setup')

export function Sidebar() {
  return (
    <nav
      aria-label="Main navigation"
      style={{
        width: '200px',
        flexShrink: 0,
        backgroundColor: 'var(--color-surface)',
        borderRight: '1px solid var(--color-border)',
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'auto',
        padding: '12px 0',
      }}
    >
      <div
        style={{
          padding: '8px 16px 16px',
          fontSize: '13px',
          fontWeight: 600,
          letterSpacing: '0.05em',
          textTransform: 'uppercase',
          color: 'var(--color-text-muted)',
          borderBottom: '1px solid var(--color-border)',
          marginBottom: '8px',
        }}
      >
        LSPDFR Manager
      </div>

      {NAV_ROUTES.map((route) => (
        <NavLink
          key={route.path}
          to={route.path}
          end={route.path === '/'}
          style={({ isActive }) => ({
            display: 'block',
            padding: '7px 16px',
            color: isActive ? 'var(--color-accent)' : 'var(--color-text)',
            backgroundColor: isActive
              ? 'var(--color-surface-raised)'
              : 'transparent',
            textDecoration: 'none',
            fontSize: '13px',
            borderLeft: isActive
              ? '2px solid var(--color-accent)'
              : '2px solid transparent',
          })}
        >
          {route.label}
        </NavLink>
      ))}
    </nav>
  )
}
