import { Panel, Page, StatusBadge } from './Page'

interface StubPageProps {
  label: string
  path: string
  sourceView: string
}

export function StubPage({ label, path, sourceView }: StubPageProps) {
  return (
    <Page
      kicker="Migration queue"
      title={label}
      description="This route is reserved for the React migration and remains backed by the existing WPF surface until its workflow is ported."
      actions={<StatusBadge tone="neutral">Queued</StatusBadge>}
    >
      <Panel>
        <div className="grid gap-4 p-5 sm:grid-cols-2">
          <div>
            <div className="text-xs font-bold uppercase tracking-wider text-zinc-600">Route</div>
            <code className="mt-1 block text-sm text-[var(--color-accent)]">{path}</code>
          </div>
          <div>
            <div className="text-xs font-bold uppercase tracking-wider text-zinc-600">WPF source</div>
            <code className="mt-1 block text-sm text-zinc-400">{sourceView}</code>
          </div>
        </div>
      </Panel>
    </Page>
  )
}
