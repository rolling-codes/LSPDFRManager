import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { ChevronDown, ImageIcon, Search, Star } from 'lucide-react'
import { Page, Panel, StateMessage, StatusBadge } from '../components/ui/Page'
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

  if (isLoading) return <StateMessage title="Loading library" description="Reading installed mods, metadata, and current enablement state." />

  if (isError) {
    return (
      <StateMessage
        tone="danger"
        title="Failed to load library"
        description={error instanceof Error ? error.message : 'Unknown error'}
      />
    )
  }

  const mods = data?.mods ?? []
  const enabledCount = mods.filter((mod) => mod.isEnabled).length

  return (
    <Page
      kicker="Inventory"
      title="Mod Library"
      description="Filter installed mods, review conflicts, and toggle loadout state without leaving the command center."
      actions={
        <>
          <StatusBadge tone="neutral">{data?.total ?? 0} total</StatusBadge>
          <StatusBadge tone="success">{enabledCount} enabled</StatusBadge>
        </>
      }
    >
      <Panel>
        <div className="flex flex-wrap gap-2 p-4">
          <div className="relative min-w-60 flex-1">
            <Search className="pointer-events-none absolute left-3 top-2.5 text-zinc-500" size={15} />
            <input
              className="input w-full pl-9"
              placeholder="Search mods"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>
          <select
            className="input w-44"
            value={typeFilter}
            onChange={(e) => setTypeFilter(e.target.value)}
          >
            {MOD_TYPES.map((t) => (
              <option key={t} value={t}>{t}</option>
            ))}
          </select>
          <select
            className="input w-40"
            value={enabledFilter}
            onChange={(e) => setEnabledFilter(e.target.value as typeof enabledFilter)}
          >
            <option value="all">All states</option>
            <option value="enabled">Enabled</option>
            <option value="disabled">Disabled</option>
          </select>
        </div>
      </Panel>

      {mods.length === 0 ? (
        <StateMessage title="No mods found" description="Try clearing filters or run a scan from the desktop app." />
      ) : (
        <Panel>
          <div className="divide-y divide-zinc-800/70">
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
        </Panel>
      )}
    </Page>
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
    <div>
      <div className="flex items-center gap-3 px-4 py-3 hover:bg-zinc-950/35">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-md border border-zinc-800 bg-zinc-950">
          {mod.thumbnailUrl ? (
            <img src={mod.thumbnailUrl} alt={mod.name} className="w-full h-full object-cover" />
          ) : (
            <ImageIcon size={17} className="text-zinc-600" />
          )}
        </div>

        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-sm font-medium text-zinc-100 truncate">{mod.name}</span>
            {mod.hasConflict && (
              <StatusBadge tone="danger">Conflict</StatusBadge>
            )}
            {mod.isFavorite && (
              <Star size={14} className="shrink-0 fill-yellow-400 text-yellow-400" />
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

        <div className="flex items-center gap-3 shrink-0">
          <button
            type="button"
            role="switch"
            aria-checked={mod.isEnabled}
            onClick={() => onToggleEnabled(!mod.isEnabled)}
            className="toggle-track"
            data-checked={mod.isEnabled}
          >
            <span className="toggle-thumb" />
          </button>

          <button
            onClick={onToggleExpand}
            className="btn-secondary h-8 w-8 p-0"
            aria-label={expanded ? 'Collapse' : 'Expand'}
          >
            <ChevronDown size={16} className={`transition-transform ${expanded ? 'rotate-180' : ''}`} />
          </button>
        </div>
      </div>

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
