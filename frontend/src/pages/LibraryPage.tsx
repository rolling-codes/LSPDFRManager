import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { fetchMods, toggleMod, updateModNotes } from '../lib/api/library'
import type { InstalledModDto, ModsListResponse } from '../types/library'

const MOD_TYPES = ['All', 'Plugin', 'VehicleDlc', 'EupUniform', 'Script', 'Other']

export default function LibraryPage() {
  const queryClient = useQueryClient()
  const [search, setSearch] = useState('')
  const [typeFilter, setTypeFilter] = useState('All')
  const [enabledFilter, setEnabledFilter] = useState<'all' | 'enabled' | 'disabled'>('all')
  const [expandedId, setExpandedId] = useState<string | null>(null)

  const params = {
    search: search || undefined,
    type: typeFilter !== 'All' ? typeFilter : undefined,
    enabled:
      enabledFilter === 'enabled' ? true
      : enabledFilter === 'disabled' ? false
      : undefined,
  }

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['mods', params],
    queryFn: () => fetchMods(params),
  })

  const toggleMutation = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) => toggleMod(id, enabled),
    onMutate: async ({ id, enabled }) => {
      await queryClient.cancelQueries({ queryKey: ['mods', params] })
      const previous = queryClient.getQueryData<ModsListResponse>(['mods', params])
      queryClient.setQueryData<ModsListResponse>(['mods', params], (old) => {
        if (!old) return old
        return {
          ...old,
          mods: old.mods.map((m) => (m.id === id ? { ...m, isEnabled: enabled } : m)),
        }
      })
      return { previous }
    },
    onError: (_err, _vars, ctx) => {
      if (ctx?.previous) {
        queryClient.setQueryData(['mods', params], ctx.previous)
      }
    },
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey: ['mods'] })
    },
  })

  if (isLoading) return <div className="p-6 text-zinc-400">Loading library…</div>

  if (isError) {
    return (
      <div className="p-6 text-red-400">
        Failed to load library: {error instanceof Error ? error.message : 'Unknown error'}
      </div>
    )
  }

  const mods = data?.mods ?? []

  return (
    <div className="p-6 space-y-4 h-full overflow-y-auto">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Mod Library</h1>
        <span className="text-sm text-zinc-500">{data?.total ?? 0} mods</span>
      </div>

      {/* Filters */}
      <div className="flex flex-wrap gap-2">
        <input
          className="input flex-1 min-w-40"
          placeholder="Search mods…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <select
          className="input w-40"
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value)}
        >
          {MOD_TYPES.map((t) => (
            <option key={t} value={t}>{t}</option>
          ))}
        </select>
        <select
          className="input w-36"
          value={enabledFilter}
          onChange={(e) => setEnabledFilter(e.target.value as typeof enabledFilter)}
        >
          <option value="all">All states</option>
          <option value="enabled">Enabled</option>
          <option value="disabled">Disabled</option>
        </select>
      </div>

      {mods.length === 0 ? (
        <p className="text-zinc-500">No mods found.</p>
      ) : (
        <div className="space-y-2">
          {mods.map((mod) => (
            <ModRow
              key={mod.id}
              mod={mod}
              expanded={expandedId === mod.id}
              onToggleExpand={() => setExpandedId(expandedId === mod.id ? null : mod.id)}
              onToggleEnabled={(enabled) => toggleMutation.mutate({ id: mod.id, enabled })}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function ModRow({
  mod,
  expanded,
  onToggleExpand,
  onToggleEnabled,
}: {
  mod: InstalledModDto
  expanded: boolean
  onToggleExpand: () => void
  onToggleEnabled: (enabled: boolean) => void
}) {
  const queryClient = useQueryClient()
  const [notesValue, setNotesValue] = useState(mod.notes)
  const [notesSaving, setNotesSaving] = useState(false)

  async function handleSaveNotes() {
    setNotesSaving(true)
    try {
      await updateModNotes(mod.id, notesValue)
      queryClient.invalidateQueries({ queryKey: ['mods'] })
    } finally {
      setNotesSaving(false)
    }
  }

  return (
    <div className="rounded-lg border border-zinc-800 bg-zinc-900">
      <div className="flex items-center gap-3 px-4 py-3">
        {/* Thumbnail */}
        <div className="shrink-0 w-10 h-10 rounded bg-zinc-800 flex items-center justify-center overflow-hidden">
          {mod.thumbnailUrl ? (
            <img src={mod.thumbnailUrl} alt={mod.name} className="w-full h-full object-cover" />
          ) : (
            <span className="text-zinc-600 text-xs">IMG</span>
          )}
        </div>

        {/* Main info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-zinc-100 truncate">{mod.name}</span>
            {mod.hasConflict && (
              <span className="shrink-0 text-xs rounded px-1.5 py-0.5 bg-red-900 text-red-300">Conflict</span>
            )}
            {mod.isFavorite && (
              <span className="shrink-0 text-xs text-yellow-400">★</span>
            )}
          </div>
          <div className="flex items-center gap-2 mt-0.5">
            <span
              className="text-xs rounded px-1.5 py-0.5 font-medium"
              style={{ backgroundColor: mod.typeColor + '33', color: mod.typeColor }}
            >
              {mod.typeLabel || mod.type}
            </span>
            {mod.version && <span className="text-xs text-zinc-500">v{mod.version}</span>}
            {mod.author && <span className="text-xs text-zinc-500">by {mod.author}</span>}
            {mod.totalSizeDisplay && <span className="text-xs text-zinc-600">{mod.totalSizeDisplay}</span>}
          </div>
        </div>

        {/* Controls */}
        <div className="flex items-center gap-3 shrink-0">
          {/* Enable/disable toggle */}
          <span
            role="switch"
            aria-checked={mod.isEnabled}
            onClick={() => onToggleEnabled(!mod.isEnabled)}
            className={`relative inline-flex h-5 w-9 shrink-0 items-center rounded-full transition-colors cursor-pointer ${
              mod.isEnabled ? 'bg-blue-600' : 'bg-zinc-600'
            }`}
          >
            <span
              className={`inline-block h-3.5 w-3.5 rounded-full bg-white shadow transition-transform ${
                mod.isEnabled ? 'translate-x-4' : 'translate-x-1'
              }`}
            />
          </span>

          {/* Expand chevron */}
          <button
            onClick={onToggleExpand}
            className="text-zinc-500 hover:text-zinc-300 transition-colors"
            aria-label={expanded ? 'Collapse' : 'Expand'}
          >
            <svg
              className={`w-4 h-4 transition-transform ${expanded ? 'rotate-180' : ''}`}
              fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}
            >
              <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
            </svg>
          </button>
        </div>
      </div>

      {/* Expanded notes area */}
      {expanded && (
        <div className="border-t border-zinc-800 px-4 py-3 space-y-2">
          <span className="text-xs text-zinc-500">
            Installed {new Date(mod.installedAt).toLocaleDateString()} · Priority {mod.loadOrderPriority} · Score {mod.detectionScore}
          </span>
          <div className="flex gap-2">
            <input
              className="input flex-1 text-sm"
              placeholder="Notes…"
              value={notesValue}
              onChange={(e) => setNotesValue(e.target.value)}
            />
            <button
              className="btn-secondary text-sm"
              onClick={handleSaveNotes}
              disabled={notesSaving || notesValue === mod.notes}
            >
              {notesSaving ? 'Saving…' : 'Save'}
            </button>
          </div>
        </div>
      )}
    </div>
  )
}
