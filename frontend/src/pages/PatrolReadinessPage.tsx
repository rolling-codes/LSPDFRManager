import { useQuery } from '@tanstack/react-query'
import { fetchPatrolReadiness } from '../lib/api/patrol-readiness'

export default function PatrolReadinessPage() {
  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ['patrol-readiness'],
    queryFn: fetchPatrolReadiness,
    staleTime: 30_000,
  })

  if (isLoading) {
    return <div className="p-6 text-zinc-400">Running patrol readiness checks…</div>
  }

  if (isError) {
    return (
      <div className="p-6 space-y-3">
        <div className="text-red-400">Failed to run patrol readiness checks.</div>
        <button className="btn-secondary" onClick={() => void refetch()}>Retry</button>
      </div>
    )
  }

  return (
    <div className="p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Patrol Readiness</h1>
        <button
          className="btn-secondary"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          {isFetching ? 'Checking…' : 'Re-check'}
        </button>
      </div>

      {/* Ready / Not Ready banner */}
      {data && (
        <div
          className={[
            'rounded-lg px-4 py-3 text-sm font-semibold',
            data.isReady
              ? 'bg-green-900/30 border border-green-700 text-green-400'
              : 'bg-red-900/30 border border-red-700 text-red-400',
          ].join(' ')}
        >
          {data.isReady
            ? '✓ Patrol Ready — all required components found.'
            : '✗ Not Ready — resolve blocking issues before launching LSPDFR.'}
        </div>
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
    </div>
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
      heading: 'text-red-400',
      icon: '✕',
      iconColor: 'text-red-500',
      border: 'border-red-900/40',
    },
    warning: {
      heading: 'text-yellow-400',
      icon: '⚠',
      iconColor: 'text-yellow-500',
      border: 'border-yellow-900/40',
    },
    passing: {
      heading: 'text-green-400',
      icon: '✓',
      iconColor: 'text-green-500',
      border: 'border-green-900/40',
    },
  }[variant]

  return (
    <section>
      <h2 className={`mb-2 text-sm font-semibold uppercase tracking-wide ${palette.heading}`}>
        {title}
      </h2>
      <ul className={`rounded-lg border divide-y divide-zinc-800/60 ${palette.border}`}>
        {items.map((item, i) => (
          <li key={i} className="flex items-start gap-3 px-4 py-2.5">
            <span className={`mt-0.5 text-sm font-bold shrink-0 ${palette.iconColor}`}>
              {palette.icon}
            </span>
            <span className="text-sm text-zinc-300">{item}</span>
          </li>
        ))}
      </ul>
    </section>
  )
}
