import type { ReactNode } from 'react'

interface PageProps {
  kicker?: string
  title: string
  description?: string
  actions?: ReactNode
  children: ReactNode
}

export function Page({ kicker, title, description, actions, children }: PageProps) {
  return (
    <div className="page-shell page-stack">
      <header className="page-header">
        <div>
          {kicker && <p className="page-kicker">{kicker}</p>}
          <h1 className="page-title">{title}</h1>
          {description && <p className="page-description">{description}</p>}
        </div>
        {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
      </header>
      {children}
    </div>
  )
}

interface PanelProps {
  title?: string
  meta?: ReactNode
  children: ReactNode
  className?: string
}

export function Panel({ title, meta, children, className = '' }: PanelProps) {
  return (
    <section className={`panel ${className}`}>
      {(title || meta) && (
        <div className="panel-header">
          {title && <h2 className="section-title">{title}</h2>}
          {meta && <div>{meta}</div>}
        </div>
      )}
      {children}
    </section>
  )
}

interface StateMessageProps {
  title: string
  description?: string
  tone?: 'neutral' | 'danger'
  action?: ReactNode
}

export function StateMessage({
  title,
  description,
  tone = 'neutral',
  action,
}: StateMessageProps) {
  return (
    <div className="page-shell">
      <div
        className={[
          'panel flex min-h-48 flex-col items-center justify-center gap-3 px-6 py-10 text-center',
          tone === 'danger' ? 'border-red-900/50' : '',
        ].join(' ')}
      >
        <h1 className={`text-base font-semibold ${tone === 'danger' ? 'text-red-300' : 'text-zinc-100'}`}>
          {title}
        </h1>
        {description && <p className="max-w-md text-sm text-zinc-400">{description}</p>}
        {action}
      </div>
    </div>
  )
}

interface StatusBadgeProps {
  children: ReactNode
  tone?: 'success' | 'warning' | 'danger' | 'neutral'
}

export function StatusBadge({ children, tone = 'neutral' }: StatusBadgeProps) {
  return <span className={`status-pill status-${tone}`}>{children}</span>
}
