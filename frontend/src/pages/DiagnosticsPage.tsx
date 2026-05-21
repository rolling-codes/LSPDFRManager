import { useQuery } from '@tanstack/react-query'
import { fetchDiagnostics } from '../lib/api/diagnostics'
import type { DiagnosticFindingDto } from '../types/diagnostics'

const SEVERITY_STYLES: Record<string, string> = {
  Ok: 'bg-green-900/40 text-green-400',
  Info: 'bg-blue-900/40 text-blue-400',
  Warning: 'bg-yellow-900/40 text-yellow-400',
  Error: 'bg-red-900/40 text-red-400',
  Critical: 'bg-red-950 text-red-300 font-bold',
}

export default function DiagnosticsPage() {
  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['diagnostics'],
    queryFn: fetchDiagnostics,
    staleTime: 0,
    enabled: true,
  })

  const grouped = data
    ? Object.entries(
        data.findings.reduce<Record<string, DiagnosticFindingDto[]>>((acc, f) => {
          ;(acc[f.category] ??= []).push(f)
          return acc
        }, {}),
      )
    : []

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Diagnostics</h1>
        <button
          className="btn-primary"
          onClick={() => refetch()}
          disabled={isFetching}
        >
          {isFetching ? 'Scanning…' : 'Re-run'}
        </button>
      </div>

      {isLoading && (
        <p className="text-zinc-400">Running diagnostics, this may take a moment…</p>
      )}

      {isError && (
        <p className="text-red-400">Failed to run diagnostics. Check that the local API is running.</p>
      )}

      {data && (
        <div className="flex gap-4">
          <SummaryBadge label="Errors" count={data.errorCount} color="text-red-400" />
          <SummaryBadge label="Warnings" count={data.warningCount} color="text-yellow-400" />
          <SummaryBadge label="Total" count={data.totalFindings} color="text-zinc-400" />
        </div>
      )}

      {data && data.findings.length === 0 && (
        <p className="text-green-400">No issues found. Everything looks good!</p>
      )}

      {grouped.map(([category, findings]) => (
        <section key={category}>
          <h2 className="mb-2 text-sm font-semibold uppercase tracking-wide text-zinc-500">
            {category}
          </h2>
          <div className="space-y-2">
            {findings.map((f, i) => (
              <FindingCard key={i} finding={f} />
            ))}
          </div>
        </section>
      ))}
    </div>
  )
}

function SummaryBadge({ label, count, color }: { label: string; count: number; color: string }) {
  return (
    <div className="rounded border border-zinc-700 bg-zinc-900 px-3 py-2 text-center">
      <div className={`text-lg font-bold ${color}`}>{count}</div>
      <div className="text-xs text-zinc-500">{label}</div>
    </div>
  )
}

function FindingCard({ finding: f }: { finding: DiagnosticFindingDto }) {
  const badgeClass = SEVERITY_STYLES[f.severity] ?? 'bg-zinc-800 text-zinc-400'
  return (
    <div className="rounded-lg border border-zinc-700 bg-zinc-900 px-4 py-3 space-y-1">
      <div className="flex items-start gap-2">
        <span className={`shrink-0 rounded px-2 py-0.5 text-xs font-semibold ${badgeClass}`}>
          {f.severity}
        </span>
        <span className="text-sm text-zinc-100">{f.title}</span>
      </div>
      {f.detail && (
        <p className="text-xs text-zinc-400 pl-1">{f.detail}</p>
      )}
      {f.affectedPath && (
        <p className="text-xs font-mono text-zinc-500 pl-1 truncate">{f.affectedPath}</p>
      )}
      {f.recommendedFix && (
        <p className="text-xs text-blue-400 pl-1">Fix: {f.recommendedFix}</p>
      )}
    </div>
  )
}
