import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import type { ReactNode } from 'react'
import { useState } from 'react'
import {
  Archive,
  CheckCircle2,
  Cog,
  FolderOpen,
  Globe,
  PackageOpen,
  Save,
  ShieldCheck,
  type LucideIcon,
} from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchConfig, updateConfig, validateGtaPath } from '../lib/api/config'
import type { AppConfigDto, BackupScheduleMode } from '../types/config'
import { invalidateEnvironmentQueries } from '../lib/queryInvalidation'

const BACKUP_MODES: BackupScheduleMode[] = [
  'ManualOnly',
  'EveryLaunch',
  'Daily',
  'Weekly',
  'BeforeProfileSwitch',
  'BeforeInstall',
  'BeforeSafeLaunch',
]

const BACKUP_MODE_LABELS: Record<BackupScheduleMode, string> = {
  ManualOnly: 'Manual only',
  EveryLaunch: 'Every launch',
  Daily: 'Daily',
  Weekly: 'Weekly',
  BeforeProfileSwitch: 'Before profile switch',
  BeforeInstall: 'Before install',
  BeforeSafeLaunch: 'Before safe launch',
}

export default function SettingsPage() {
  const queryClient = useQueryClient()

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['config'],
    queryFn: fetchConfig,
  })

  const [patch, setPatch] = useState<Partial<AppConfigDto>>({})
  const [gtaPathValidation, setGtaPathValidation] = useState<{ valid: boolean; error: string | null } | null>(null)
  const [saveStatus, setSaveStatus] = useState<'idle' | 'saving' | 'saved' | 'error'>('idle')

  const mutation = useMutation({
    mutationFn: updateConfig,
    onSuccess: (updated) => {
      const previousConfig = queryClient.getQueryData<AppConfigDto>(['config'])
      queryClient.setQueryData(['config'], updated)
      setPatch({})
      setSaveStatus('saved')
      setTimeout(() => setSaveStatus('idle'), 2000)

      const pathChanged = (previousConfig?.gtaPath ?? '') !== (updated.gtaPath ?? '')
      if (pathChanged) {
        void invalidateEnvironmentQueries(queryClient)
      }

      const backupPathChanged = (previousConfig?.backupPath ?? '') !== (updated.backupPath ?? '')
      if (backupPathChanged) {
        queryClient.invalidateQueries({ queryKey: ['backups'] })
      }
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

  function setNumber(key: keyof AppConfigDto, raw: string, min?: number, max?: number) {
    const n = parseInt(raw, 10)
    if (isNaN(n)) return
    const clamped = min !== undefined && n < min ? min : max !== undefined && n > max ? max : n
    set(key, clamped as AppConfigDto[typeof key])
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

      <Section icon={FolderOpen} title="Game" description="GTA V installation directory used by all mod operations.">
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
            <p className={`mt-1 text-sm ${gtaPathValidation.valid ? 'text-green-400' : 'text-red-400'}`}>
              {gtaPathValidation.valid ? 'Valid GTA V installation.' : gtaPathValidation.error}
            </p>
          )}
        </Field>
      </Section>

      <Section icon={PackageOpen} title="Install" description="How the install planner and queue behave during mod installs.">
        <Toggle
          label="Auto-backup before install"
          description="Create a library snapshot before each install operation."
          checked={form.autoBackupOnInstall}
          onChange={(v) => set('autoBackupOnInstall', v)}
        />
        <Toggle
          label="Confirm before uninstall"
          description="Show a confirmation dialog before removing a mod from disk."
          checked={form.confirmBeforeUninstall}
          onChange={(v) => set('confirmBeforeUninstall', v)}
        />
        <Toggle
          label="Launch game after install"
          description="Start GTA V automatically when the install queue finishes."
          checked={form.autoLaunchAfterInstall}
          onChange={(v) => set('autoLaunchAfterInstall', v)}
        />
        <Toggle
          label="Auto-install high-confidence detections"
          description="Skip the review step when detection confidence is High."
          checked={form.autoInstallHighConfidence}
          onChange={(v) => set('autoInstallHighConfidence', v)}
        />
        <Toggle
          label="Delete temp archive after install"
          description="Remove the source archive once it has been fully installed."
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
            onChange={(e) => setNumber('maxInstallLogEntries', e.target.value, 1, 10000)}
          />
        </Field>
        <Field label="Minimum free disk space (MB)">
          <input
            type="number"
            className="input w-32"
            min={0}
            value={form.minimumFreeDiskSpaceMb}
            onChange={(e) => setNumber('minimumFreeDiskSpaceMb', e.target.value, 0)}
          />
        </Field>
      </Section>

      <Section icon={Archive} title="Backups" description="Restore point schedule and retention policy.">
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
            className="input w-52"
            value={form.backupScheduleMode}
            onChange={(e) => set('backupScheduleMode', e.target.value as BackupScheduleMode)}
            disabled={!form.autoBackupEnabled}
          >
            {BACKUP_MODES.map((m) => (
              <option key={m} value={m}>{BACKUP_MODE_LABELS[m]}</option>
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
            onChange={(e) => setNumber('maxBackupCount', e.target.value, 1, 100)}
          />
        </Field>
        <Toggle
          label="Compress backups"
          description="Store backups as ZIP archives to reduce disk usage."
          checked={form.compressBackups}
          onChange={(v) => set('compressBackups', v)}
        />
      </Section>

      <Section icon={Globe} title="Browse API" description="External browse service for discovering and queuing mod downloads.">
        <Toggle
          label="Auto-start Browse API"
          description="Launch the browse backend when the application starts."
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
          <p className="mt-1 text-xs text-zinc-600">Must be a localhost origin, e.g. http://127.0.0.1:7100</p>
        </Field>
      </Section>

      <Section icon={Cog} title="General" description="Startup behavior, UI scale, and update preferences.">
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
            className="input w-44"
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

      {/* Sticky save footer */}
      <div className="sticky bottom-0 z-10 border-t border-zinc-800/70 bg-[var(--color-app)]/95 py-4 backdrop-blur-sm">
        <div className="flex items-center gap-4">
          <button
            className="btn-primary"
            onClick={handleSave}
            disabled={!hasChanges || saveStatus === 'saving'}
          >
            <Save size={15} />
            {saveStatus === 'saving' ? 'Saving…' : 'Save Settings'}
          </button>
          {saveStatus === 'error' && (
            <span className="text-sm text-red-400">
              {mutation.error instanceof Error ? mutation.error.message : 'Save failed.'}
            </span>
          )}
          {!hasChanges && saveStatus === 'idle' && (
            <span className="text-xs text-zinc-600">No unsaved changes</span>
          )}
        </div>
      </div>
    </Page>
  )
}

function Section({
  title,
  description,
  icon: Icon,
  children,
}: {
  title: string
  description?: string
  icon?: LucideIcon
  children: ReactNode
}) {
  return (
    <Panel>
      <div className="flex items-start gap-3 border-b border-zinc-800/60 px-5 pb-4 pt-4">
        {Icon && (
          <div className="mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-zinc-800/80 text-zinc-400">
            <Icon size={14} />
          </div>
        )}
        <div>
          <h2 className="text-sm font-semibold text-zinc-100">{title}</h2>
          {description && <p className="mt-0.5 text-xs text-zinc-500">{description}</p>}
        </div>
      </div>
      <div className="space-y-4 p-5">{children}</div>
    </Panel>
  )
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-start gap-4 max-[720px]:flex-col max-[720px]:gap-2">
      <label className="w-56 shrink-0 pt-1 text-sm text-zinc-300 max-[720px]:w-auto">{label}</label>
      <div className="flex flex-1 flex-col">{children}</div>
    </div>
  )
}

function Toggle({
  label,
  description,
  checked,
  onChange,
}: {
  label: string
  description?: string
  checked: boolean
  onChange: (v: boolean) => void
}) {
  return (
    <label className="flex cursor-pointer select-none items-start gap-3">
      <button
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        className="toggle-track mt-0.5 shrink-0"
        data-checked={checked}
      >
        <span className="toggle-thumb" />
      </button>
      <div>
        <span className="text-sm text-zinc-300">{label}</span>
        {description && <p className="mt-0.5 text-xs text-zinc-500">{description}</p>}
      </div>
    </label>
  )
}
