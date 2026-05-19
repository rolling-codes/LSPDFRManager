interface StubPageProps {
  label: string
  path: string
  sourceView: string
}

export function StubPage({ label, path, sourceView }: StubPageProps) {
  return (
    <div>
      <h1
        style={{
          fontSize: '22px',
          fontWeight: 600,
          color: 'var(--color-text)',
          margin: '0 0 8px',
        }}
      >
        {label}
      </h1>
      <p style={{ color: 'var(--color-text-muted)', margin: '0 0 4px' }}>
        Route: <code style={{ color: 'var(--color-accent)' }}>{path}</code>
      </p>
      <p style={{ color: 'var(--color-text-muted)', margin: '0 0 20px' }}>
        WPF source: <code style={{ color: 'var(--color-text-muted)' }}>{sourceView}</code>
      </p>
      <div
        style={{
          display: 'inline-block',
          padding: '6px 12px',
          backgroundColor: 'var(--color-surface-raised)',
          border: '1px solid var(--color-border)',
          borderRadius: '4px',
          fontSize: '12px',
          color: 'var(--color-text-muted)',
        }}
      >
        Not migrated yet
      </div>
    </div>
  )
}
