import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { fetchConfig, updateConfig, validateGtaPath } from '../lib/api/config'
import type { AppConfigDto, BackupScheduleMode } from '../types/config'

const BACKUP_MODES: BackupScheduleMode[] = [
  'ManualOnly',
  'EveryLaunch',
  'Daily',
  'Weekly',
  'BeforeProfileSwitch',
  'BeforeInstall',
  'BeforeSafeLaunch',
]

export default function SettingsPage() {
  const queryClient = useQueryClient()

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['config'],
    queryFn: fetchConfig,
  })

  // Tracks only user-made changes; merged with `data` at render time.
  const [patch, setPatch] = useState<Partial<AppConfigDto>>({})
  const [gtaPathValidation, setGtaPathValidation] = useState<{ valid: boolean; error: string | null } | null>(null)
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const mutation = useMutation({
    mutationFn: updateConfig,
    onSuccess: (updated) => {
      queryClient.setQueryData(['config'], updated)
      setPatch({})
      setSaveStatus('saved')
      setTimeout(() => setSaveStatus('idle'), 2000)
    },
    onError: () => setSaveStatus('error'),
  })

  if (isLoading) return <div className="p-6 text-zinc-400">Loading settings…</div>

  if (isError) {
    return (
      <div className="p-6 text-red-400">
        Failed to load settings: {error instanceof Error ? error.message : 'Unknown error'}
      </div>
    )
  }

  if (!data) return null

  const form: AppConfigDto = { ...data, ...patch }

  function set<K extends keyof AppConfigDto>(key: K, value: AppConfigDto[K]) {
    setPatch((prev) => ({ ...prev, [key]: value }))
    setSaveStatus('idle')
    if (key === 'gtaPath') setGtaPathValidation(null)
  }

  async function handleValidateGtaPath() {
    try {
      const result = await validateGtaPath(form.gtaPath)
      setGtaPathValidation(result)
    } catch {
      setGtaPathValidation({ valid: false, error: 'Validation request failed.' })
    }
  }

  function handleSave() {
    if (Object.keys(patch).length === 0) return
    setSaveStatus('saving')
    mutation.mutate(patch)
  }

  const hasChanges = Object.keys(patch).length > 0

  return (
    <div className="p-6 max-w-2xl space-y-8 overflow-y-auto h-full">
      <h1 className="text-xl font-semibold text-zinc-100">Settings</h1>

      <Section title="Game">
        <Field label="GTA V Installation Folder">
          <div className="flex gap-2">
            <input
              className="input flex-1"
              value={form.gtaPath}
              onChange={(e) => set('gtaPath', e.target.value)}
            />
            <button className="btn-secondary" onClick={handleValidateGtaPath}>
              Validate
            </button>
          </div>
          {gtaPathValidation && (
            <p className={gtaPathValidation.valid ? 'text-green-400 text-sm mt-1' : 'text-red-400 text-sm mt-1'}>
              {gtaPathValidation.valid ? 'Valid GTA V installation.' : gtaPathValidation.error}
            </p>
          )}
        </Field>
      </Section>

      <Section title="Install">
        <Toggle
          label="Auto-backup before install"
          checked={form.autoBackupOnInstall}
          onChange={(v) => set('autoBackupOnInstall', v)}
        />
        <Toggle
          label="Confirm before uninstall"
          checked={form.confirmBeforeUninstall}
          onChange={(v) => set('confirmBeforeUninstall', v)}
        />
        <Toggle
          label="Launch game after install"
          checked={form.autoLaunchAfterInstall}
          onChange={(v) => set('autoLaunchAfterInstall', v)}
        />
        <Toggle
          label="Auto-install high-confidence detections"
          checked={form.autoInstallHighConfidence}
          onChange={(v) => set('autoInstallHighConfidence', v)}
        />
        <Toggle
          label="Delete temp archive after install"
          checked={form.deleteTempAfterInstall}
          onChange={(v) => set('deleteTempAfterInstall', v)}
        />
        <Field label="Max install log entries">
          <input
            type="number"
            className="input w-32"
            min={1}
            max={10000}
            value={form.maxInstallLogEntries}
            onChange={(e) => set('maxInstallLogEntries', parseInt(e.target.value, 10))}
          />
        </Field>
        <Field label="Minimum free disk space (MB)">
          <input
            type="number"
            className="input w-32"
            min={0}
            value={form.minimumFreeDiskSpaceMb}
            onChange={(e) => set('minimumFreeDiskSpaceMb', parseInt(e.target.value, 10))}
          />
        </Field>
      </Section>

      <Section title="Backups">
        <Field label="Backup folder">
          <input
            className="input w-full"
            value={form.backupPath}
            onChange={(e) => set('backupPath', e.target.value)}
          />
        </Field>
        <Toggle
          label="Enable automatic backups"
          checked={form.autoBackupEnabled}
          onChange={(v) => set('autoBackupEnabled', v)}
        />
        <Field label="Backup schedule">
          <select
            className="input w-48"
            value={form.backupScheduleMode}
            onChange={(e) => set('backupScheduleMode', e.target.value as BackupScheduleMode)}
            disabled={!form.autoBackupEnabled}
          >
            {BACKUP_MODES.map((m) => (
              <option key={m} value={m}>{m}</option>
            ))}
          </select>
        </Field>
        <Field label="Max backups to keep">
          <input
            type="number"
            className="input w-32"
            min={1}
            max={100}
            value={form.maxBackupCount}
            onChange={(e) => set('maxBackupCount', parseInt(e.target.value, 10))}
          />
        </Field>
        <Toggle
          label="Compress backups"
          checked={form.compressBackups}
          onChange={(v) => set('compressBackups', v)}
        />
      </Section>

      <Section title="Browse API">
        <Toggle
          label="Auto-start Browse API"
          checked={form.autoStartBrowseApi}
          onChange={(v) => set('autoStartBrowseApi', v)}
        />
        <Field label="Browse API executable path">
          <input
            className="input w-full"
            value={form.browseApiPath ?? ''}
            onChange={(e) => set('browseApiPath', e.target.value || null)}
            disabled={!form.autoStartBrowseApi}
          />
        </Field>
        <Field label="Browse API base URL">
          <input
            className="input w-full"
            value={form.browseApiBaseUrl}
            onChange={(e) => set('browseApiBaseUrl', e.target.value)}
          />
        </Field>
      </Section>

      <Section title="General">
        <Toggle
          label="Show setup wizard on startup"
          checked={form.showSetupWizardOnStartup}
          onChange={(v) => set('showSetupWizardOnStartup', v)}
        />
        <Toggle
          label="Check for updates on startup"
          checked={form.checkForUpdatesOnStartup}
          onChange={(v) => set('checkForUpdatesOnStartup', v)}
        />
        <Field label="UI scale">
          <select
            className="input w-40"
            value={String(form.uiScale)}
            onChange={(e) => set('uiScale', parseFloat(e.target.value))}
          >
            <option value="0.85">Small (85%)</option>
            <option value="1">Default (100%)</option>
            <option value="1.25">Large (125%)</option>
            <option value="1.5">Extra Large (150%)</option>
          </select>
        </Field>
      </Section>

      <div className="flex items-center gap-4 pt-2 pb-8">
        <button
          className="btn-primary"
          onClick={handleSave}
          disabled={!hasChanges || saveStatus === 'saving'}
        >
          {saveStatus === 'saving' ? 'Saving…' : 'Save Settings'}
        </button>
        {saveStatus === 'saved' && <span className="text-green-400 text-sm">Settings saved.</span>}
        {saveStatus === 'error' && (
          <span className="text-red-400 text-sm">
            {mutation.error instanceof Error ? mutation.error.message : 'Save failed.'}
          </span>
        )}
      </div>
    </div>
  )
}

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="space-y-3">
      <h2 className="text-sm font-medium text-zinc-400 uppercase tracking-wider">{title}</h2>
      <div className="space-y-3">{children}</div>
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-start gap-4">
      <label className="w-52 shrink-0 text-sm text-zinc-300 pt-1">{label}</label>
      <div className="flex flex-col flex-1">{children}</div>
    </div>
  )
}

function Toggle({
  label,
  checked,
  onChange,
}: {
  label: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex items-center gap-3 cursor-pointer select-none">
      <span
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors cursor-pointer ${
          checked ? 'bg-blue-600' : 'bg-zinc-600'
        }`}
      >
        <span
          className={`inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform ${
            checked ? 'translate-x-4' : 'translate-x-1'
          }`}
        />
      </span>
      <span className="text-sm text-zinc-300">{label}</span>
    </label>
  )
}
