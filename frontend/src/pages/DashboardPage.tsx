import { useQuery } from '@tanstack/react-query'
import { fetchCompatibility } from '../lib/api/compatibility'
import type { ComponentVersionDto } from '../types/compatibility'

export default function DashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['compatibility'],
    queryFn: fetchCompatibility,
    staleTime: 60_000,
  })

  if (isLoading) {
    return <div className="p-6 text-zinc-400">Detecting components…</div>
  }

  if (isError) {
    return <div className="p-6 text-red-400">Failed to load compatibility data.</div>
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Dashboard</h1>
        {!data?.gtaPathConfigured && (
          <span className="rounded bg-yellow-900/40 px-2 py-1 text-xs text-yellow-400">
            GTA path not configured
          </span>
        )}
      </div>

      <section>
        <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-zinc-500">
          Component Versions
        </h2>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          {(data?.components ?? []).map((c) => (
            <ComponentCard key={c.name} component={c} />
          ))}
        </div>
      </section>

      {data && (
        <p className="text-xs text-zinc-600">
          Detected at {new Date(data.detectedAt).toLocaleString()}
        </p>
      )}
    </div>
  )
}

function ComponentCard({ component: c }: { component: ComponentVersionDto }) {
  return (
    <div
      className={[
        'rounded-lg border px-4 py-3 space-y-1',
        c.present
          ? 'border-zinc-700 bg-zinc-900'
          : 'border-zinc-800 bg-zinc-950 opacity-60',
      ].join(' ')}
    >
      <div className="flex items-center justify-between gap-2">
        <span className="text-sm font-medium text-zinc-100">{c.name}</span>
        <span
          className={[
            'text-xs font-semibold',
            c.present ? 'text-green-400' : 'text-zinc-600',
          ].join(' ')}
        >
          {c.present ? 'Present' : 'Not found'}
        </span>
      </div>
      {c.version && (
        <p className="text-xs text-zinc-400 font-mono">{c.version}</p>
      )}
    </div>
  )
}
