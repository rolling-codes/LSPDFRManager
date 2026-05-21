import { useState } from 'react'
import { useQuery, useMutation } from '@tanstack/react-query'
import { RefreshCcw, ShieldAlert, Trash2 } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchCleanupScan, applyCleanup } from '../lib/api/cleanup'
import type { RemovalCandidateDto, CleanupApplyResponse } from '../types/cleanup'

const RISK_STYLES: Record<string, string> = {
  Low: 'text-green-400',
  Medium: 'text-yellow-400',
  High: 'text-red-400',
  Advanced: 'text-red-300',
}

export default function CleanupPage() {
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [mode, setMode] = useState<'Safe' | 'Aggressive'>('Safe')
  const [result, setResult] = useState<CleanupApplyResponse | null>(null)

  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['cleanup-scan'],
    queryFn: fetchCleanupScan,
    staleTime: 0,
  })

  const applyMutation = useMutation({
    mutationFn: applyCleanup,
    onSuccess: (res) => {
      setResult(res)
      setSelected(new Set())
      refetch()
    },
  })

  const candidates = data?.candidates ?? []
  const selectableCandidates = candidates.filter((c) => !c.isBlocked)

  const toggleOne = (path: string) => {
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(path)) next.delete(path)
      else next.add(path)
      return next
    })
  }

  const selectAll = () => setSelected(new Set(selectableCandidates.map((c) => c.relativePath)))
  const deselectAll = () => setSelected(new Set())

  const handleApply = () => {
    if (selected.size === 0) return
    const confirmed = window.confirm(
      `Delete ${selected.size} file(s) in ${mode} mode? A backup will be created first.`,
    )
    if (!confirmed) return
    applyMutation.mutate({ relativePaths: [...selected], mode })
  }

  const totalSelectedBytes = candidates
    .filter((c) => selected.has(c.relativePath))
    .reduce((sum, c) => sum + c.sizeBytes, 0)

  return (
    <Page
      kicker="Maintenance"
      title="Cleanup"
      description="Review removable files, choose a risk mode, and apply cleanup only after a backup is created."
      actions={
        <button className="btn-secondary" onClick={() => refetch()} disabled={isFetching}>
          <RefreshCcw size={15} className={isFetching ? 'animate-spin' : ''} />
          {isFetching ? 'Scanning' : 'Scan'}
        </button>
      }
    >

      {isLoading && <StateMessage title="Scanning for removable files" description="The cleanup scanner is reviewing known safe candidates." />}
      {isError && <StateMessage tone="danger" title="Scan failed" description="Ensure GTA V path is configured." />}

      {result && (
        <Panel className={result.success ? 'border-green-900/50' : 'border-red-900/50'}>
          <div className={`p-4 text-sm ${result.success ? 'text-green-300' : 'text-red-300'}`}>
          {result.success
            ? `Deleted ${result.filesDeleted} file(s) successfully.`
            : `Cleanup failed: ${result.error}`}
          </div>
        </Panel>
      )}

      {data && candidates.length === 0 && (
        <StateMessage title="No removable files found" description="The current install does not expose cleanup candidates." />
      )}

      {candidates.length > 0 && (
        <>
          <Panel>
          <div className="flex items-center gap-4 flex-wrap p-4">
            <div className="flex gap-2">
              <button className="btn-secondary text-xs" onClick={selectAll}>
                Select All
              </button>
              <button className="btn-secondary text-xs" onClick={deselectAll}>
                Deselect All
              </button>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs text-zinc-500">Mode:</span>
              {(['Safe', 'Aggressive'] as const).map((m) => (
                <button
                  key={m}
                  onClick={() => setMode(m)}
                  className={`rounded px-2 py-1 text-xs font-semibold transition-colors ${
                    mode === m
                      ? m === 'Safe'
                        ? 'bg-green-700 text-white'
                        : 'bg-red-700 text-white'
                      : 'bg-zinc-800 text-zinc-400 hover:bg-zinc-700'
                  }`}
                >
                  {m}
                </button>
              ))}
            </div>
            <div className="ml-auto flex items-center gap-3">
              {selected.size > 0 && (
                <StatusBadge tone="warning">
                  {selected.size} selected ({formatBytes(totalSelectedBytes)})
                </StatusBadge>
              )}
              <button
                className="btn-primary"
                disabled={selected.size === 0 || applyMutation.isPending}
                onClick={handleApply}
              >
                {applyMutation.isPending ? <RefreshCcw size={15} className="animate-spin" /> : <Trash2 size={15} />}
                {applyMutation.isPending ? 'Cleaning' : 'Clean Selected'}
              </button>
            </div>
          </div>
          </Panel>

          <Panel title="Candidates" meta={<StatusBadge tone="neutral">Total on disk: {formatBytes(data?.totalSizeBytes ?? 0)}</StatusBadge>}>
          <div className="divide-y divide-zinc-800/70">
            {candidates.map((c) => (
              <CandidateRow
                key={c.relativePath}
                candidate={c}
                checked={selected.has(c.relativePath)}
                onToggle={() => toggleOne(c.relativePath)}
              />
            ))}
          </div>
          </Panel>
        </>
      )}
    </Page>
  )
}

function CandidateRow({
  candidate: c,
  checked,
  onToggle,
}: {
  candidate: RemovalCandidateDto
  checked: boolean
  onToggle: () => void
}) {
  const riskColor = RISK_STYLES[c.riskLevel] ?? 'text-zinc-400'
  return (
    <div className={`flex items-start gap-3 px-4 py-3 ${c.isBlocked ? 'opacity-60' : 'hover:bg-zinc-950/35'}`}>
      <input
        type="checkbox"
        checked={checked}
        onChange={onToggle}
        disabled={c.isBlocked}
        className="mt-0.5 accent-blue-500"
      />
      <div className="min-w-0 flex-1 space-y-0.5">
        <div className="flex items-center gap-2 flex-wrap">
          <span className="text-sm font-mono text-zinc-100 truncate">{c.relativePath}</span>
          {c.isBlocked && (
            <span className="inline-flex items-center gap-1 rounded bg-red-900/40 px-1.5 py-0.5 text-xs text-red-400">
              <ShieldAlert size={12} />
              Blocked
            </span>
          )}
        </div>
        <div className="flex gap-3 text-xs">
          <span className="text-zinc-500">{c.classification}</span>
          <span className={riskColor}>{c.riskLevel} risk</span>
          <span className="text-zinc-500">{c.sizeDisplay}</span>
        </div>
      </div>
    </div>
  )
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(2)} GB`
}
