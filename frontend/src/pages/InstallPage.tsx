import { useState } from 'react'
import { CheckCircle2, HardDriveDownload, Loader2, XCircle } from 'lucide-react'
import { Page, Panel, StatusBadge } from '../components/ui/Page'
import { startInstall } from '../lib/api/install'
import { useJob } from '../lib/hooks/useJob'
import type { InstallResultDto } from '../types/install'

export default function InstallPage() {
  const [sourcePath, setSourcePath] = useState('')
  const [jobId, setJobId] = useState<string | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const { jobStatus, isPolling } = useJob(jobId)

  async function handleInstall() {
    if (!sourcePath.trim()) return
    setSubmitError(null)
    setJobId(null)
    try {
      const { jobId: id } = await startInstall({ sourcePath: sourcePath.trim() })
      setJobId(id)
    } catch (e) {
      setSubmitError(e instanceof Error ? e.message : 'Failed to start install.')
    }
  }

  const isRunning = isPolling
  const isDone = jobStatus?.state === 'Completed'
  const isFailed = jobStatus?.state === 'Failed'

  let result: InstallResultDto | null = null
  if (isDone && jobStatus?.resultJson) {
    try {
      result = JSON.parse(jobStatus.resultJson) as InstallResultDto
    } catch {
      // ignore parse errors
    }
  }

  return (
    <Page
      kicker="Workflow"
      title="Install Mod"
      description="Start a guarded install from an archive or extracted folder and monitor job progress."
      actions={<StatusBadge tone={isRunning ? 'warning' : 'neutral'}>{isRunning ? 'Running' : 'Idle'}</StatusBadge>}
    >
      <Panel title="Install Source">
        <div className="space-y-4 p-5">
          <label className="block text-sm font-medium text-zinc-300">Archive or folder path</label>
          <div className="flex flex-col gap-2 sm:flex-row">
            <input
              className="input flex-1"
              placeholder="C:\Downloads\my-mod.zip"
              value={sourcePath}
              onChange={(e) => setSourcePath(e.target.value)}
              disabled={isRunning}
            />
            <button
              className="btn-primary"
              onClick={handleInstall}
              disabled={isRunning || !sourcePath.trim()}
            >
              {isRunning ? <Loader2 size={16} className="animate-spin" /> : <HardDriveDownload size={16} />}
              {isRunning ? 'Installing' : 'Install'}
            </button>
          </div>
        </div>
      </Panel>

      {submitError && (
        <Panel className="border-red-900/50">
          <p className="p-4 text-sm text-red-300">{submitError}</p>
        </Panel>
      )}

      {jobId && jobStatus && (
        <Panel
          title="Job Status"
          className={isFailed ? 'border-red-900/50' : isDone ? 'border-green-900/50' : ''}
          meta={
            <StatusBadge tone={isFailed ? 'danger' : isDone ? 'success' : 'warning'}>
              {isDone ? <CheckCircle2 size={13} /> : isFailed ? <XCircle size={13} /> : <Loader2 size={13} className="animate-spin" />}
              {isDone ? 'Complete' : isFailed ? 'Failed' : 'Installing'}
            </StatusBadge>
          }
        >
          <div className="space-y-3 p-4">
            {isRunning && (
              <div className="h-2 w-full overflow-hidden rounded-full bg-zinc-800">
                <div
                  className="h-2 rounded-full bg-[var(--color-accent)] transition-all"
                  style={{ width: `${jobStatus.progressPct}%` }}
                />
              </div>
            )}

            {isRunning && <p className="text-sm text-zinc-400">{jobStatus.progressPct}% complete</p>}

            {isDone && result && (
              <p className="text-sm text-green-300">
                {result.filesInstalled} file{result.filesInstalled !== 1 ? 's' : ''} installed.
              </p>
            )}

            {isFailed && jobStatus.error && (
              <p className="text-sm text-red-400">{jobStatus.error}</p>
            )}
          </div>
        </Panel>
      )}
    </Page>
  )
}
