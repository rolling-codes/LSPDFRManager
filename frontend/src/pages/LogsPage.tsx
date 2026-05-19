import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { fetchAvailableLogs, fetchLogLines } from '../lib/api/logs'

export default function LogsPage() {
  const [selected, setSelected] = useState<string | null>(null)
  const [tail, setTail] = useState(200)

  const { data: available, isLoading: logsLoading } = useQuery({
    queryKey: ['logs'],
    queryFn: fetchAvailableLogs,
  })

  const { data: lines, isLoading: linesLoading, isError } = useQuery({
    queryKey: ['log-lines', selected, tail],
    queryFn: () => fetchLogLines(selected!, tail),
    enabled: selected !== null,
  })

  if (logsLoading) {
    return <div className="p-6 text-zinc-400">Loading logs…</div>
  }

  const logs = available?.logs ?? []

  const activeLog = selected ?? (logs[0]?.name ?? null)
  if (activeLog !== selected && activeLog !== null) {
    setSelected(activeLog)
  }

  return (
    <div className="flex h-full">
      <aside className="w-48 shrink-0 border-r border-zinc-800 p-3 space-y-1">
        <p className="px-2 pb-1 text-xs font-semibold uppercase tracking-wide text-zinc-500">
          Log Files
        </p>
        {logs.length === 0 && (
          <p className="px-2 text-xs text-zinc-600">No logs found</p>
        )}
        {logs.map((log) => (
          <button
            key={log.name}
            onClick={() => setSelected(log.name)}
            className={[
              'w-full rounded px-2 py-1.5 text-left text-sm transition-colors',
              selected === log.name
                ? 'bg-zinc-700 text-zinc-100'
                : 'text-zinc-400 hover:bg-zinc-800 hover:text-zinc-100',
            ].join(' ')}
          >
            {log.label}
          </button>
        ))}
      </aside>

      <main className="flex-1 flex flex-col overflow-hidden">
        <div className="flex items-center justify-between border-b border-zinc-800 px-4 py-2">
          <span className="text-sm font-medium text-zinc-300">
            {lines?.label ?? 'Select a log'}
          </span>
          <div className="flex items-center gap-2">
            <span className="text-xs text-zinc-500">
              {lines ? `${lines.lines.length} / ${lines.totalLines} lines` : ''}
            </span>
            <select
              value={tail}
              onChange={(e) => setTail(Number(e.target.value))}
              className="rounded border border-zinc-700 bg-zinc-900 px-2 py-0.5 text-xs text-zinc-300"
            >
              <option value={100}>Last 100</option>
              <option value={200}>Last 200</option>
              <option value={500}>Last 500</option>
              <option value={1000}>Last 1000</option>
            </select>
          </div>
        </div>

        <div className="flex-1 overflow-auto p-4">
          {linesLoading && <p className="text-zinc-400 text-sm">Loading…</p>}
          {isError && <p className="text-red-400 text-sm">Failed to load log.</p>}
          {lines && (
            <pre className="text-xs text-zinc-300 font-mono whitespace-pre-wrap leading-5">
              {lines.lines.join('\n')}
            </pre>
          )}
        </div>
      </main>
    </div>
  )
}
