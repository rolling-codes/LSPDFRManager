import { useState } from 'react'
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
    <div className="p-6 max-w-xl space-y-6 overflow-y-auto h-full">
      <h1 className="text-xl font-semibold text-zinc-100">Install Mod</h1>

      <div className="space-y-3">
        <label className="block text-sm text-zinc-300">Archive or folder path</label>
        <input
          className="input w-full"
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
          {isRunning ? 'Installing…' : 'Install'}
        </button>
      </div>

      {submitError && (
        <p className="text-red-400 text-sm">{submitError}</p>
      )}

      {jobId && jobStatus && (
        <div className={`rounded-lg border px-4 py-3 space-y-3 ${
          isFailed ? 'border-red-800 bg-red-950/30' :
          isDone   ? 'border-green-800 bg-green-950/30' :
                     'border-zinc-700 bg-zinc-900'
        }`}>
          <div className="flex items-center justify-between text-sm">
            <span className={
              isFailed ? 'text-red-400' :
              isDone   ? 'text-green-400' :
                         'text-zinc-300'
            }>
              {isDone ? 'Install complete' : isFailed ? 'Install failed' : `Installing… ${jobStatus.progressPct}%`}
            </span>
            {isRunning && (
              <span className="text-zinc-500 font-mono">{jobStatus.progressPct}%</span>
            )}
          </div>

          {isRunning && (
            <div className="h-1.5 w-full rounded-full bg-zinc-800">
              <div
                className="h-1.5 rounded-full bg-blue-500 transition-all"
                style={{ width: `${jobStatus.progressPct}%` }}
              />
            </div>
          )}

          {isDone && result && (
            <p className="text-sm text-green-300">
              {result.filesInstalled} file{result.filesInstalled !== 1 ? 's' : ''} installed.
            </p>
          )}

          {isFailed && jobStatus.error && (
            <p className="text-sm text-red-400">{jobStatus.error}</p>
          )}
        </div>
      )}
    </div>
  )
}
