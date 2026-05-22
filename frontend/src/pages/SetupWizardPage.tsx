import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import {
  CheckCircle2,
  ChevronRight,
  FolderOpen,
  Loader2,
  Shield,
  XCircle,
} from 'lucide-react'
import { Panel, StatusBadge } from '../components/ui/Page'
import { validateGtaPath, updateConfig } from '../lib/api/config'
import { invalidateEnvironmentQueries } from '../lib/queryInvalidation'

// ─── Common candidate GTA V install locations ─────────────────────────────
const CANDIDATE_PATHS = [
  'C:\\Program Files\\Rockstar Games\\Grand Theft Auto V',
  'C:\\Program Files (x86)\\Rockstar Games\\Grand Theft Auto V',
  'C:\\Program Files\\Steam\\steamapps\\common\\Grand Theft Auto V',
  'C:\\Program Files (x86)\\Steam\\steamapps\\common\\Grand Theft Auto V',
  'D:\\SteamLibrary\\steamapps\\common\\Grand Theft Auto V',
  'D:\\Games\\Grand Theft Auto V',
  'E:\\Games\\Grand Theft Auto V',
]

type Step = 1 | 2 | 3

export default function SetupWizardPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  const [step, setStep] = useState<Step>(1)
  const [gtaPath, setGtaPath] = useState('')
  const [validating, setValidating] = useState(false)
  const [saving, setSaving] = useState(false)
  const [validation, setValidation] = useState<{ valid: boolean; error: string | null } | null>(null)
  const [saveError, setSaveError] = useState<string | null>(null)

  // ── Step 1 helpers ────────────────────────────────────────────────────────
  async function handleAutoDetect() {
    setValidating(true)
    setValidation(null)
    // Try each candidate; use validate API to find the first valid one
    for (const candidate of CANDIDATE_PATHS) {
      try {
        const result = await validateGtaPath(candidate)
        if (result.valid) {
          setGtaPath(candidate)
          setValidation(result)
          setValidating(false)
          return
        }
      } catch {
        // ignore individual failures
      }
    }
    setValidating(false)
    setValidation({ valid: false, error: 'Could not auto-detect GTA V. Please enter the path manually.' })
  }

  async function handleValidate() {
    if (!gtaPath.trim()) return
    setValidating(true)
    setValidation(null)
    try {
      const result = await validateGtaPath(gtaPath.trim())
      setValidation(result)
    } catch {
      setValidation({ valid: false, error: 'Validation request failed.' })
    } finally {
      setValidating(false)
    }
  }

  // ── Step 3 helpers ────────────────────────────────────────────────────────
  async function handleSaveAndFinish() {
    setSaving(true)
    setSaveError(null)
    try {
      const updated = await updateConfig({ gtaPath: gtaPath.trim() })
      queryClient.setQueryData(['config'], updated)
      await invalidateEnvironmentQueries(queryClient)
      navigate('/')
    } catch (e) {
      setSaveError(e instanceof Error ? e.message : 'Save failed.')
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="page-shell page-stack">
      {/* ── Header ─────────────────────────────────────────────────────── */}
      <header className="page-header">
        <div>
          <p className="page-kicker">First-time setup</p>
          <h1 className="page-title">Setup Wizard</h1>
          <p className="page-description">
            Tell LSPDFR Manager where GTA V is installed. This takes less than a minute.
          </p>
        </div>
        <div className="flex shrink-0 items-center gap-2">
          <StatusBadge tone="neutral">Step {step} of 3</StatusBadge>
        </div>
      </header>

      {/* ── Step indicators ────────────────────────────────────────────── */}
      <div className="flex items-center gap-2">
        {(['Find GTA V', 'Validate path', 'Confirm'] as const).map((label, i) => {
          const n = (i + 1) as Step
          const done = step > n
          const active = step === n
          return (
            <div key={n} className="flex items-center gap-2">
              <div
                className={[
                  'flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold transition-colors',
                  done
                    ? 'bg-[var(--color-success)] text-black'
                    : active
                      ? 'bg-[var(--color-accent)] text-white'
                      : 'bg-zinc-800 text-zinc-500',
                ].join(' ')}
              >
                {done ? <CheckCircle2 size={14} /> : n}
              </div>
              <span
                className={`text-sm ${active ? 'text-zinc-100 font-medium' : done ? 'text-zinc-400' : 'text-zinc-600'}`}
              >
                {label}
              </span>
              {i < 2 && <ChevronRight size={14} className="text-zinc-700" />}
            </div>
          )
        })}
      </div>

      {/* ═══════════════════════════════════════════════════════════════════
          STEP 1 — Find GTA V
      ═══════════════════════════════════════════════════════════════════ */}
      {step === 1 && (
        <Panel title="Locate GTA V Installation">
          <div className="space-y-5 p-5">
            <div className="flex items-start gap-3 rounded-md border border-zinc-700 bg-zinc-950/40 p-4">
              <Shield size={20} className="mt-0.5 shrink-0 text-[var(--color-accent)]" />
              <p className="text-sm text-zinc-300">
                LSPDFR Manager needs to know where Grand Theft Auto V is installed to detect
                components, install mods, and manage plugins safely.
              </p>
            </div>

            <div className="space-y-2">
              <label className="block text-sm font-medium text-zinc-300">
                GTA V installation folder
              </label>
              <div className="flex gap-2">
                <input
                  className="input flex-1"
                  placeholder="C:\Program Files\Rockstar Games\Grand Theft Auto V"
                  value={gtaPath}
                  onChange={(e) => {
                    setGtaPath(e.target.value)
                    setValidation(null)
                  }}
                />
                <button
                  className="btn-secondary"
                  onClick={handleAutoDetect}
                  disabled={validating}
                >
                  {validating ? (
                    <Loader2 size={15} className="animate-spin" />
                  ) : (
                    <FolderOpen size={15} />
                  )}
                  Auto-detect
                </button>
              </div>

              {validation && (
                <p
                  className={`flex items-center gap-1.5 text-sm ${validation.valid ? 'text-green-400' : 'text-red-400'}`}
                >
                  {validation.valid ? (
                    <CheckCircle2 size={14} />
                  ) : (
                    <XCircle size={14} />
                  )}
                  {validation.valid ? 'Valid GTA V installation found.' : validation.error}
                </p>
              )}
            </div>

            <div className="flex justify-end pt-2">
              <button
                className="btn-primary"
                disabled={!gtaPath.trim()}
                onClick={() => setStep(2)}
              >
                Next: Validate
                <ChevronRight size={15} />
              </button>
            </div>
          </div>
        </Panel>
      )}

      {/* ═══════════════════════════════════════════════════════════════════
          STEP 2 — Validate path
      ═══════════════════════════════════════════════════════════════════ */}
      {step === 2 && (
        <Panel title="Validate Path">
          <div className="space-y-5 p-5">
            <div className="rounded-md border border-zinc-700 bg-zinc-950/40 p-4">
              <div className="text-xs font-bold uppercase tracking-wider text-zinc-500">
                Path to validate
              </div>
              <code className="mt-1 block break-all text-sm text-[var(--color-accent)]">
                {gtaPath}
              </code>
            </div>

            {!validation && (
              <button
                className="btn-primary"
                onClick={handleValidate}
                disabled={validating}
              >
                {validating ? (
                  <Loader2 size={15} className="animate-spin" />
                ) : (
                  <CheckCircle2 size={15} />
                )}
                {validating ? 'Validating…' : 'Validate now'}
              </button>
            )}

            {validation && (
              <div
                className={[
                  'flex items-start gap-3 rounded-md border p-4',
                  validation.valid
                    ? 'border-green-900/60 bg-green-950/30'
                    : 'border-red-900/60 bg-red-950/30',
                ].join(' ')}
              >
                {validation.valid ? (
                  <CheckCircle2 size={18} className="mt-0.5 shrink-0 text-green-400" />
                ) : (
                  <XCircle size={18} className="mt-0.5 shrink-0 text-red-400" />
                )}
                <div>
                  <p
                    className={`text-sm font-medium ${validation.valid ? 'text-green-300' : 'text-red-300'}`}
                  >
                    {validation.valid ? 'Valid GTA V installation' : 'Validation failed'}
                  </p>
                  {!validation.valid && validation.error && (
                    <p className="mt-1 text-sm text-red-400">{validation.error}</p>
                  )}
                  {validation.valid && (
                    <p className="mt-1 text-sm text-zinc-400">
                      GTA V executable was found. You can proceed to the next step.
                    </p>
                  )}
                </div>
              </div>
            )}

            <div className="flex justify-between pt-2">
              <button
                className="btn-secondary"
                onClick={() => {
                  setStep(1)
                  setValidation(null)
                }}
              >
                Back
              </button>
              <button
                className="btn-primary"
                disabled={!validation?.valid}
                onClick={() => setStep(3)}
              >
                Next: Confirm
                <ChevronRight size={15} />
              </button>
            </div>
          </div>
        </Panel>
      )}

      {/* ═══════════════════════════════════════════════════════════════════
          STEP 3 — Confirm & save
      ═══════════════════════════════════════════════════════════════════ */}
      {step === 3 && (
        <Panel title="Confirm & Finish">
          <div className="space-y-5 p-5">
            <div className="space-y-3">
              <ConfirmRow label="GTA V path" value={gtaPath} tone="success" />
              <ConfirmRow label="Status" value="Valid GTA V installation detected" tone="success" />
            </div>

            <div className="rounded-md border border-zinc-700 bg-zinc-950/40 p-4 text-sm text-zinc-400">
              Clicking <strong className="text-zinc-200">Finish</strong> will save this path and
              immediately refresh the Dashboard, Patrol Readiness, and all other component
              status views.
            </div>

            {saveError && (
              <p className="flex items-center gap-1.5 text-sm text-red-400">
                <XCircle size={14} />
                {saveError}
              </p>
            )}

            <div className="flex justify-between pt-2">
              <button
                className="btn-secondary"
                onClick={() => setStep(2)}
                disabled={saving}
              >
                Back
              </button>
              <button
                className="btn-primary"
                onClick={handleSaveAndFinish}
                disabled={saving}
              >
                {saving ? (
                  <Loader2 size={15} className="animate-spin" />
                ) : (
                  <CheckCircle2 size={15} />
                )}
                {saving ? 'Saving…' : 'Finish setup'}
              </button>
            </div>
          </div>
        </Panel>
      )}
    </div>
  )
}

function ConfirmRow({
  label,
  value,
  tone,
}: {
  label: string
  value: string
  tone: 'success' | 'neutral'
}) {
  return (
    <div className="flex items-start gap-4">
      <span className="w-28 shrink-0 text-xs font-bold uppercase tracking-wider text-zinc-500 pt-0.5">
        {label}
      </span>
      <span
        className={`break-all text-sm ${tone === 'success' ? 'text-green-300' : 'text-zinc-300'}`}
      >
        {value}
      </span>
    </div>
  )
}
