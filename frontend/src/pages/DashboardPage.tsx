import { useQuery } from '@tanstack/react-query'
import { Link } from 'react-router-dom'
import {
  AlertTriangle,
  CheckCircle2,
  ClipboardCheck,
  Gauge,
  HardDriveDownload,
  Library,
  XCircle,
} from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchCompatibility } from '../lib/api/compatibility'
import type { ComponentVersionDto } from '../types/compatibility'

export default function DashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['compatibility'],
    queryFn: fetchCompatibility,
    staleTime: 60_000,
  })

  if (isLoading) {
    return (
      <StateMessage
        title="Detecting components"
        description="Checking GTA V, RPH, LSPDFR, and supporting runtime components."
      />
    )
  }

  if (isError) {
    return (
      <StateMessage
        tone="danger"
        title="Failed to load compatibility data"
        description="The local API did not return component status."
      />
    )
  }

  const components = data?.components ?? []
  const present = components.filter((c) => c.present).length
  const missing = components.length - present
  const allPresent = missing === 0 && components.length > 0
  const lastChecked = data
    ? new Date(data.detectedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : '--'

  return (
    <Page
      kicker="Overview"
      title="Dashboard"
      description="A quick read on launch readiness, detected components, and the current GTA V environment."
      actions={
        <StatusBadge tone={data?.gtaPathConfigured ? 'success' : 'warning'}>
          {data?.gtaPathConfigured ? (
            <CheckCircle2 size={13} />
          ) : (
            <AlertTriangle size={13} />
          )}
          {data?.gtaPathConfigured ? 'GTA path configured' : 'GTA path missing'}
        </StatusBadge>
      }
    >
      {!data?.gtaPathConfigured && (
        <div className="rounded-lg border border-amber-900/60 bg-amber-950/20 p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex gap-3">
              <AlertTriangle className="h-5 w-5 shrink-0 text-amber-500" />
              <div>
                <h4 className="text-sm font-semibold text-amber-200">GTA V Path Not Configured</h4>
                <p className="mt-1 text-xs text-zinc-400">
                  LSPDFR Manager requires your GTA V installation directory to manage mods and run compatibility checks.
                </p>
              </div>
            </div>
            <Link
              to="/setup"
              className="btn-primary self-start whitespace-nowrap text-xs"
              style={{ background: '#d97706', borderColor: '#d97706', color: '#000', fontWeight: 600 }}
            >
              Configure GTA V Path
            </Link>
          </div>
        </div>
      )}

      {/* Launch readiness hero */}
      <Panel>
        <div className="flex items-start justify-between gap-6 p-6">
          <div>
            <p className="mb-2 text-xs font-bold uppercase tracking-wider text-zinc-500">
              Launch Readiness
            </p>
            <h2
              className={`text-2xl font-bold leading-tight ${
                components.length === 0
                  ? 'text-zinc-500'
                  : allPresent
                  ? 'text-[var(--color-success)]'
                  : 'text-[var(--color-warning)]'
              }`}
            >
              {components.length === 0
                ? 'No data'
                : allPresent
                ? 'Ready to Launch'
                : `${missing} Component${missing > 1 ? 's' : ''} Missing`}
            </h2>
            <p className="mt-1.5 text-sm text-zinc-500">
              {present} of {components.length} components detected&ensp;·&ensp;last checked {lastChecked}
            </p>
          </div>

          <div
            className={`flex h-14 w-14 shrink-0 items-center justify-center rounded-full ${
              allPresent
                ? 'bg-emerald-500/10 text-[var(--color-success)]'
                : 'bg-amber-500/10 text-[var(--color-warning)]'
            }`}
          >
            {allPresent ? <CheckCircle2 size={26} /> : <AlertTriangle size={26} />}
          </div>
        </div>

        <div className="flex flex-wrap gap-2 border-t border-zinc-800/60 px-6 py-3">
          <Link to="/library" className="quick-link">
            <Library size={12} />
            Library
          </Link>
          <Link to="/install" className="quick-link">
            <HardDriveDownload size={12} />
            Install Mod
          </Link>
          <Link to="/patrol-readiness" className="quick-link">
            <ClipboardCheck size={12} />
            Patrol Check
          </Link>
          <Link to="/diagnostics" className="quick-link">
            <Gauge size={12} />
            Diagnostics
          </Link>
        </div>
      </Panel>

      {/* Component grid */}
      <Panel
        title="Component Versions"
        meta={
          <StatusBadge tone={missing > 0 ? 'warning' : 'success'}>
            {components.length} checked
          </StatusBadge>
        }
      >
        <div className="grid gap-3 p-4 sm:grid-cols-2 xl:grid-cols-3">
          {components.map((c) => (
            <ComponentCard key={c.name} component={c} />
          ))}
        </div>
      </Panel>
    </Page>
  )
}

function ComponentCard({ component: c }: { component: ComponentVersionDto }) {
  return (
    <div
      className={[
        'overflow-hidden rounded-md border',
        c.present ? 'border-zinc-700/80' : 'border-zinc-800/60',
      ].join(' ')}
    >
      <div className={`h-0.5 ${c.present ? 'bg-[var(--color-success)]' : 'bg-zinc-800'}`} />
      <div className="space-y-1.5 bg-zinc-950/30 px-4 py-3">
        <div className="flex items-center justify-between gap-2">
          <span className="truncate text-sm font-medium text-zinc-100">{c.name}</span>
          <span
            className={`flex shrink-0 items-center gap-1 text-xs font-semibold ${
              c.present ? 'text-[var(--color-success)]' : 'text-zinc-600'
            }`}
          >
            {c.present ? <CheckCircle2 size={12} /> : <XCircle size={12} />}
            {c.present ? 'Present' : 'Not found'}
          </span>
        </div>
        {c.version && (
          <p className="font-mono text-xs text-zinc-400">{c.version}</p>
        )}
        {c.hash && (
          <p className="truncate font-mono text-xs text-zinc-600" title={c.hash}>
            {c.hash.slice(0, 16)}&hellip;
          </p>
        )}
      </div>
    </div>
  )
}
