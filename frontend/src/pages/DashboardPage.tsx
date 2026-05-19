import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, RefreshCcw, XCircle } from 'lucide-react'
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
    return <StateMessage title="Detecting components" description="Checking GTA V, RPH, LSPDFR, and supporting runtime components." />
  }

  if (isError) {
    return <StateMessage tone="danger" title="Failed to load compatibility data" description="The local API did not return component status." />
  }

  const components = data?.components ?? []
  const present = components.filter((component) => component.present).length
  const missing = components.length - present

  return (
    <Page
      kicker="Overview"
      title="Dashboard"
      description="A quick read on launch readiness, detected components, and the current GTA V environment."
      actions={
        data?.gtaPathConfigured ? (
          <StatusBadge tone="success">
            <CheckCircle2 size={13} />
            GTA path configured
          </StatusBadge>
        ) : (
          <StatusBadge tone="warning">
            <AlertTriangle size={13} />
            GTA path missing
          </StatusBadge>
        )
      }
    >
      <div className="grid gap-4 md:grid-cols-3">
        <Metric label="Detected" value={present} tone="success" />
        <Metric label="Missing" value={missing} tone={missing > 0 ? 'danger' : 'neutral'} />
        <Metric
          label="Last checked"
          value={data ? new Date(data.detectedAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }) : '--'}
          tone="neutral"
        />
      </div>

      <Panel
        title="Component Versions"
        meta={<StatusBadge tone={missing > 0 ? 'warning' : 'success'}>{components.length} checked</StatusBadge>}
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
  const Icon = c.present ? CheckCircle2 : XCircle

  return (
    <div
      className={[
        'rounded-md border px-4 py-3 space-y-2',
        c.present
          ? 'border-zinc-700 bg-zinc-950/35'
          : 'border-zinc-800 bg-zinc-950/70',
      ].join(' ')}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-zinc-100">{c.name}</span>
        <span className={`flex items-center gap-1 text-xs font-semibold ${c.present ? 'text-green-400' : 'text-zinc-600'}`}>
          <Icon size={13} />
          {c.present ? 'Present' : 'Not found'}
        </span>
      </div>
      {c.version && (
        <p className="text-xs text-zinc-400 font-mono">{c.version}</p>
      )}
    </div>
  )
}

function Metric({
  label,
  value,
  tone,
}: {
  label: string
  value: string | number
  tone: 'success' | 'danger' | 'neutral'
}) {
  const toneClass = {
    success: 'text-[var(--color-success)]',
    danger: 'text-[var(--color-danger)]',
    neutral: 'text-zinc-100',
  }[tone]

  return (
    <Panel>
      <div className="flex items-center justify-between p-4">
        <div>
          <div className="text-xs font-bold uppercase tracking-wider text-zinc-600">{label}</div>
          <div className={`mt-1 text-2xl font-semibold ${toneClass}`}>{value}</div>
        </div>
        <RefreshCcw size={18} className="text-zinc-600" />
      </div>
    </Panel>
  )
}
