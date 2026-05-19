import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { BookOpen } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchAvailableLogs, fetchLogLines } from '../lib/api/logs'

export default function LogsPage() {
  const [selected, setSelected] = useState<string | null>(null)
  const [tail, setTail] = useState(200)

  const { data: available, isLoading: logsLoading } = useQuery({
    queryKey: ['logs'],
    queryFn: fetchAvailableLogs,
  })

  const logs = available?.logs ?? []
  const activeLog = selected ?? logs[0]?.name ?? null

  const { data: lines, isLoading: linesLoading, isError } = useQuery({
    queryKey: ['log-lines', activeLog, tail],
    queryFn: () => fetchLogLines(activeLog!, tail),
    enabled: activeLog !== null,
  })

  if (logsLoading) {
    return <StateMessage title="Loading logs" description="Finding available app and game log files." />
  }

  return (
    <Page
      kicker="Telemetry"
      title="Log Viewer"
      description="Read recent logs in a stable two-pane viewer without leaving the manager."
      actions={<StatusBadge tone="neutral">{logs.length} files</StatusBadge>}
    >
      <Panel className="min-h-[560px] overflow-hidden">
        <div className="grid min-h-[560px] grid-cols-[220px_minmax(0,1fr)] max-[760px]:grid-cols-1">
          <aside className="border-r border-zinc-800 p-3 max-[760px]:border-b max-[760px]:border-r-0">
            <p className="px-2 pb-2 text-xs font-semibold uppercase tracking-wide text-zinc-500">
              Log Files
            </p>
            {logs.length === 0 && (
              <p className="px-2 text-xs text-zinc-600">No logs found</p>
            )}
            <div className="space-y-1">
              {logs.map((log) => (
                <button
                  key={log.name}
                  onClick={() => setSelected(log.name)}
                  className={[
                    'flex w-full items-center gap-2 rounded-md px-2 py-2 text-left text-sm transition-colors',
                    activeLog === log.name
                      ? 'bg-zinc-800 text-zinc-100'
                      : 'text-zinc-400 hover:bg-zinc-900 hover:text-zinc-100',
                  ].join(' ')}
                >
                  <BookOpen size={15} className="text-zinc-500" />
                  <span className="truncate">{log.label}</span>
                </button>
              ))}
            </div>
          </aside>

          <main className="flex min-w-0 flex-col overflow-hidden">
            <div className="flex items-center justify-between border-b border-zinc-800 px-4 py-3">
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
                  className="input py-1 text-xs"
                >
                  <option value={100}>Last 100</option>
                  <option value={200}>Last 200</option>
                  <option value={500}>Last 500</option>
                  <option value={1000}>Last 1000</option>
                </select>
              </div>
            </div>

            <div className="min-h-0 flex-1 overflow-auto bg-zinc-950/40 p-4">
              {linesLoading && <p className="text-sm text-zinc-400">Loading...</p>}
              {isError && <p className="text-sm text-red-400">Failed to load log.</p>}
              {lines && (
                <pre className="whitespace-pre-wrap font-mono text-xs leading-5 text-zinc-300">
                  {lines.lines.join('\n')}
                </pre>
              )}
            </div>
          </main>
        </div>
      </Panel>
    </Page>
  )
}
