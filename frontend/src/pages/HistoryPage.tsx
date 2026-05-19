import { useQuery } from '@tanstack/react-query'
import { FileClock } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchHistory } from '../lib/api/history'
import type { ChangeHistoryEntryDto } from '../types/history'

export default function HistoryPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['history'],
    queryFn: () => fetchHistory(50, 0),
  })

  if (isLoading) {
    return <StateMessage title="Loading history" description="Reading recent install and change events." />
  }

  if (isError) {
    return (
      <StateMessage
        tone="danger"
        title="Failed to load history"
        description={error instanceof Error ? error.message : 'Unknown error'}
      />
    )
  }

  const entries = data?.entries ?? []

  return (
    <Page
      kicker="Audit"
      title="Change History"
      description="A chronological record of install, uninstall, enable, disable, and file operations."
      actions={<StatusBadge tone="neutral">{data?.total ?? 0} entries</StatusBadge>}
    >

      {entries.length === 0 ? (
        <StateMessage title="No history recorded yet" description="Changes will appear here after installs or library operations run." />
      ) : (
        <Panel>
          <div className="divide-y divide-zinc-800/70">
          {entries.map((entry) => (
            <HistoryRow key={entry.id} entry={entry} />
          ))}
          </div>
        </Panel>
      )}
    </Page>
  )
}

function HistoryRow({ entry }: { entry: ChangeHistoryEntryDto }) {
  const date = new Date(entry.occurredAt).toLocaleString()

  return (
    <div className="px-4 py-3 hover:bg-zinc-950/35">
      <div className="flex items-center justify-between gap-2">
        <span className="flex min-w-0 items-center gap-2 text-sm font-medium text-zinc-100">
          <FileClock size={15} className="shrink-0 text-zinc-500" />
          <span className="truncate">{entry.description}</span>
        </span>
        <span className="shrink-0 text-xs text-zinc-500">{date}</span>
      </div>
      <div className="flex items-center gap-3 text-xs text-zinc-400">
        <span className="rounded bg-zinc-800 px-1.5 py-0.5 font-mono">{entry.action}</span>
        {entry.affectedFile && <span className="truncate">{entry.affectedFile}</span>}
      </div>
      {entry.detail && (
        <p className="text-xs text-zinc-500 truncate">{entry.detail}</p>
      )}
    </div>
  )
}
