import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useState } from 'react'
import { CheckCircle2, Save, ShieldCheck } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
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

  if (isLoading) return <StateMessage title="Loading settings" description="Reading persisted desktop configuration." />

  if (isError) {
    return (
      <StateMessage
        tone="danger"
        title="Failed to load settings"
        description={error instanceof Error ? error.message : 'Unknown error'}
      />
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
    <Page
      kicker="Configuration"
      title="Settings"
      description="Tune install behavior, backup policy, Browse API access, and general startup preferences."
      actions={
        <>
          {hasChanges && <StatusBadge tone="warning">Unsaved changes</StatusBadge>}
          {saveStatus === 'saved' && (
            <StatusBadge tone="success">
              <CheckCircle2 size={13} />
              Saved
            </StatusBadge>
          )}
        </>
      }
    >

      <Section title="Game">
        <Field label="GTA V Installation Folder">
          <div className="flex gap-2">
            <input
              className="input flex-1"
              value={form.gtaPath}
              onChange={(e) => set('gtaPath', e.target.value)}
            />
            <button className="btn-secondary" onClick={handleValidateGtaPath}>
              <ShieldCheck size={15} />
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
          <Save size={15} />
          {saveStatus === 'saving' ? 'Saving' : 'Save Settings'}
        </button>
        {saveStatus === 'error' && (
          <span className="text-red-400 text-sm">
            {mutation.error instanceof Error ? mutation.error.message : 'Save failed.'}
          </span>
        )}
      </div>
    </Page>
  )
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Panel title={title}>
      <div className="space-y-4 p-5">{children}</div>
    </Panel>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start gap-4 max-[720px]:flex-col max-[720px]:gap-2">
      <label className="w-56 shrink-0 text-sm text-zinc-300 pt-1 max-[720px]:w-auto">{label}</label>
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
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className="toggle-track"
        data-checked={checked}
      >
        <span className="toggle-thumb" />
      </button>
      <span className="text-sm text-zinc-300">{label}</span>
    </label>
  )
}
