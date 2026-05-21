import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, CheckCircle2, RefreshCcw, XCircle } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
import { fetchPatrolReadiness } from '../lib/api/patrol-readiness'

export default function PatrolReadinessPage() {
  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['patrol-readiness'],
    queryFn: fetchPatrolReadiness,
    staleTime: 30_000,
  })

  if (isLoading) {
    return <StateMessage title="Running patrol readiness checks" description="Validating the core launch components and current install state." />
  }

  if (isError) {
    return (
      <StateMessage
        tone="danger"
        title="Failed to run patrol readiness checks"
        description="The readiness endpoint could not complete."
        action={<button className="btn-secondary" onClick={() => void refetch()}><RefreshCcw size={15} />Retry</button>}
      />
    )
  }

  return (
    <Page
      kicker="Pre-flight"
      title="Patrol Readiness"
      description="Check the install before launching LSPDFR and separate blockers from lower-risk warnings."
      actions={
        <button
          className="btn-secondary"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          <RefreshCcw size={15} className={isFetching ? 'animate-spin' : ''} />
          {isFetching ? 'Checking' : 'Re-check'}
        </button>
      }
    >

      {data && (
        <Panel className={data.isReady ? 'border-green-900/50' : 'border-red-900/50'}>
          <div className="flex items-center justify-between gap-4 p-5">
            <div>
              <div className={`text-lg font-semibold ${data.isReady ? 'text-green-300' : 'text-red-300'}`}>
          {data.isReady
                  ? 'Patrol ready'
                  : 'Not ready'}
              </div>
              <p className="mt-1 text-sm text-zinc-400">
                {data.isReady
                  ? 'All required components were found.'
                  : 'Resolve blocking issues before launching LSPDFR.'}
              </p>
            </div>
            <StatusBadge tone={data.isReady ? 'success' : 'danger'}>
              {data.isReady ? <CheckCircle2 size={13} /> : <XCircle size={13} />}
              {data.isReady ? 'Ready' : 'Blocked'}
            </StatusBadge>
          </div>
        </Panel>
      )}

      {data && data.blocking.length > 0 && (
        <CheckSection title="Blocking Issues" items={data.blocking} variant="blocking" />
      )}

      {data && data.warnings.length > 0 && (
        <CheckSection title="Warnings" items={data.warnings} variant="warning" />
      )}

      {data && data.passing.length > 0 && (
        <CheckSection title="Passing" items={data.passing} variant="passing" />
      )}
    </Page>
  )
}

function CheckSection({
  title,
  items,
  variant,
}: {
  title: string
  items: string[]
  variant: 'blocking' | 'warning' | 'passing'
}) {
  const palette = {
    blocking: {
      heading: 'text-red-300',
      Icon: XCircle,
      iconColor: 'text-red-500',
      border: 'border-red-900/40',
    },
    warning: {
      heading: 'text-yellow-300',
      Icon: AlertTriangle,
      iconColor: 'text-yellow-500',
      border: 'border-yellow-900/40',
    },
    passing: {
      heading: 'text-green-300',
      Icon: CheckCircle2,
      iconColor: 'text-green-500',
      border: 'border-green-900/40',
    },
  }[variant]
  const Icon = palette.Icon

  return (
    <Panel title={title} className={palette.border}>
      <ul className="divide-y divide-zinc-800/60">
        {items.map((item, i) => (
          <li key={i} className="flex items-start gap-3 px-4 py-2.5">
            <Icon size={15} className={`mt-0.5 shrink-0 ${palette.iconColor}`} />
            <span className="text-sm text-zinc-300">{item}</span>
          </li>
        ))}
      </ul>
    </Panel>
  )
}
