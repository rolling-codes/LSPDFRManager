import { useState } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { fetchBackups, startBackup, restoreBackup, deleteBackup } from '../lib/api/backups'
import { useJob } from '../lib/hooks/useJob'
import type { BackupFileDto } from '../types/backups'

export default function BackupsPage() {
  const queryClient = useQueryClient()
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['backups'],
    queryFn: fetchBackups,
  })

  const [backupJobId, setBackupJobId] = useState<string | null>(null)
  const [restoreJobId, setRestoreJobId] = useState<string | null>(null)
  const [restoreTarget, setRestoreTarget] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const { jobStatus: backupJob, isPolling: backupPolling } = useJob(backupJobId)
  const { jobStatus: restoreJob, isPolling: restorePolling } = useJob(restoreJobId)

  // Refetch list once a backup job completes
  if (backupJob?.state === 'Completed') {
    queryClient.invalidateQueries({ queryKey: ['backups'] })
  }

  async function handleCreateBackup() {
    setActionError(null)
    setBackupJobId(null)
    try {
      const { jobId } = await startBackup()
      setBackupJobId(jobId)
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Failed to start backup.')
    }
  }

  async function handleRestore(backup: BackupFileDto) {
    if (!window.confirm(`Restore from "${backup.fileName}"? This will overwrite current app data.`)) return
    setActionError(null)
    setRestoreJobId(null)
    setRestoreTarget(backup.fileName)
    try {
      const { jobId } = await restoreBackup({ fileName: backup.fileName })
      setRestoreJobId(jobId)
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Failed to start restore.')
    }
  }

  async function handleDelete(backup: BackupFileDto) {
    if (!window.confirm(`Delete backup "${backup.fileName}"? This cannot be undone.`)) return
    setActionError(null)
    try {
      await deleteBackup(backup.fileName)
      queryClient.invalidateQueries({ queryKey: ['backups'] })
    } catch (e) {
      setActionError(e instanceof Error ? e.message : 'Failed to delete backup.')
    }
  }

  if (isLoading) {
    return <div className="p-6 text-zinc-400">Loading backups…</div>
  }

  if (isError) {
    return (
      <div className="p-6 text-red-400">
        Failed to load backups: {error instanceof Error ? error.message : 'Unknown error'}
      </div>
    )
  }

  const backups = data?.backups ?? []

  return (
    <div className="p-6 space-y-4 overflow-y-auto h-full">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Backups</h1>
        <button
          className="btn-primary"
          onClick={handleCreateBackup}
          disabled={backupPolling}
        >
          {backupPolling ? 'Backing up…' : 'Create Backup'}
        </button>
      </div>

      {actionError && (
        <p className="text-red-400 text-sm">{actionError}</p>
      )}

      {backupJobId && backupJob && (
        <JobStatusBanner
          label="Backup"
          state={backupJob.state}
          progressPct={backupJob.progressPct}
          error={backupJob.error}
        />
      )}

      {restoreJobId && restoreJob && (
        <JobStatusBanner
          label={`Restore from ${restoreTarget ?? ''}`}
          state={restoreJob.state}
          progressPct={restoreJob.progressPct}
          error={restoreJob.error}
        />
      )}

      {backups.length === 0 ? (
        <p className="text-zinc-500">No backups found. Create one to get started.</p>
      ) : (
        <div className="space-y-2">
          {backups.map((backup) => (
            <BackupRow
              key={backup.fileName}
              backup={backup}
              onRestore={handleRestore}
              onDelete={handleDelete}
              restoreDisabled={restorePolling}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function BackupRow({
  backup,
  onRestore,
  onDelete,
  restoreDisabled,
}: {
  backup: BackupFileDto
  onRestore: (b: BackupFileDto) => void
  onDelete: (b: BackupFileDto) => void
  restoreDisabled: boolean
}) {
  const date = new Date(backup.lastWriteUtc).toLocaleString()

  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-900 px-4 py-3 flex items-center justify-between gap-4">
      <div className="flex flex-col gap-0.5 min-w-0">
        <span className="text-sm font-medium text-zinc-100 truncate">{backup.fileName}</span>
        <span className="text-xs text-zinc-500">{backup.sizeDisplay} · {date}</span>
      </div>
      <div className="flex gap-2 shrink-0">
        <button
          className="btn-secondary"
          onClick={() => onRestore(backup)}
          disabled={restoreDisabled}
        >
          Restore
        </button>
        <button
          className="btn-secondary"
          onClick={() => onDelete(backup)}
        >
          Delete
        </button>
      </div>
    </div>
  )
}

function JobStatusBanner({
  label,
  state,
  progressPct,
  error,
}: {
  label: string
  state: string
  progressPct: number
  error: string | null
}) {
  const isRunning = state === 'Running' || state === 'Pending'
  const isError = state === 'Failed'
  const isDone = state === 'Completed'

  return (
    <div className={`rounded-lg border px-4 py-3 space-y-2 ${
      isError ? 'border-red-800 bg-red-950/30' :
      isDone  ? 'border-green-800 bg-green-950/30' :
                'border-zinc-700 bg-zinc-900'
    }`}>
      <div className="flex items-center justify-between text-sm">
        <span className={isError ? 'text-red-400' : isDone ? 'text-green-400' : 'text-zinc-300'}>
          {label}: {state}
        </span>
        {isRunning && <span className="text-zinc-500">{progressPct}%</span>}
      </div>
      {isRunning && (
        <div className="h-1.5 w-full rounded-full bg-zinc-800">
          <div
            className="h-1.5 rounded-full bg-blue-500 transition-all"
            style={{ width: `${progressPct}%` }}
          />
        </div>
      )}
      {isError && error && (
        <p className="text-xs text-red-400">{error}</p>
      )}
      {isDone && (
        <p className="text-xs text-green-400">Completed successfully.</p>
      )}
    </div>
  )
}
