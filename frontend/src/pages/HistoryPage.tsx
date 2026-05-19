import { useQuery } from '@tanstack/react-query'
import { fetchHistory } from '../lib/api/history'
import type { ChangeHistoryEntryDto } from '../types/history'

export default function HistoryPage() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['history'],
    queryFn: () => fetchHistory(50, 0),
  })

  if (isLoading) {
    return <div className="p-6 text-zinc-400">Loading history…</div>
  }

  if (isError) {
    return (
      <div className="p-6 text-red-400">
        Failed to load history: {error instanceof Error ? error.message : 'Unknown error'}
      </div>
    )
  }

  const entries = data?.entries ?? []

  return (
    <div className="p-6 space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Change History</h1>
        <span className="text-sm text-zinc-500">{data?.total ?? 0} entries</span>
      </div>

      {entries.length === 0 ? (
        <p className="text-zinc-500">No history recorded yet.</p>
      ) : (
        <div className="space-y-2">
          {entries.map((entry) => (
            <HistoryRow key={entry.id} entry={entry} />
          ))}
        </div>
      )}
    </div>
  )
}

function HistoryRow({ entry }: { entry: ChangeHistoryEntryDto }) {
  const date = new Date(entry.occurredAt).toLocaleString()

  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-900 px-4 py-3 space-y-1">
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-zinc-100">{entry.description}</span>
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
