import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { fetchProfiles, createProfile, deleteProfile } from '../lib/api/profiles'
import type { ModProfileDto } from '../types/profiles'

export default function ProfilesPage() {
  const queryClient = useQueryClient()
  const [showNewForm, setShowNewForm] = useState(false)
  const [newName, setNewName] = useState('')
  const [newNotes, setNewNotes] = useState('')

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['profiles'],
    queryFn: fetchProfiles,
  })

  const createMutation = useMutation({
    mutationFn: () => createProfile({ name: newName.trim(), notes: newNotes.trim() || undefined }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profiles'] })
      setShowNewForm(false)
      setNewName('')
      setNewNotes('')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => deleteProfile(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['profiles'] })
    },
  })

  function handleDelete(profile: ModProfileDto) {
    if (window.confirm(`Delete profile "${profile.name}"? This cannot be undone.`)) {
      deleteMutation.mutate(profile.id)
    }
  }

  if (isLoading) return <div className="p-6 text-zinc-400">Loading profiles…</div>

  if (isError) {
    return (
      <div className="p-6 text-red-400">
        Failed to load profiles: {error instanceof Error ? error.message : 'Unknown error'}
      </div>
    )
  }

  const profiles = data?.profiles ?? []
  const activeProfileId = data?.activeProfileId ?? null

  return (
    <div className="p-6 space-y-4 h-full overflow-y-auto">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold text-zinc-100">Profiles</h1>
        <button className="btn-primary" onClick={() => setShowNewForm((v) => !v)}>
          {showNewForm ? 'Cancel' : 'New Profile'}
        </button>
      </div>

      {/* New profile form */}
      {showNewForm && (
        <div className="rounded-lg border border-zinc-700 bg-zinc-900 p-4 space-y-3">
          <h2 className="text-sm font-medium text-zinc-300">Create Profile</h2>
          <div className="space-y-2">
            <input
              className="input w-full"
              placeholder="Profile name"
              value={newName}
              onChange={(e) => setNewName(e.target.value)}
            />
            <input
              className="input w-full"
              placeholder="Notes (optional)"
              value={newNotes}
              onChange={(e) => setNewNotes(e.target.value)}
            />
          </div>
          <div className="flex gap-2">
            <button
              className="btn-primary"
              disabled={!newName.trim() || createMutation.isPending}
              onClick={() => createMutation.mutate()}
            >
              {createMutation.isPending ? 'Creating…' : 'Create'}
            </button>
            <button className="btn-secondary" onClick={() => setShowNewForm(false)}>
              Cancel
            </button>
          </div>
          {createMutation.isError && (
            <p className="text-red-400 text-sm">
              {createMutation.error instanceof Error ? createMutation.error.message : 'Create failed.'}
            </p>
          )}
        </div>
      )}

      {profiles.length === 0 ? (
        <p className="text-zinc-500">No profiles yet. Create one to get started.</p>
      ) : (
        <div className="space-y-2">
          {profiles.map((profile) => (
            <ProfileRow
              key={profile.id}
              profile={profile}
              isActive={profile.id === activeProfileId}
              onDelete={() => handleDelete(profile)}
              deleteDisabled={deleteMutation.isPending}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function ProfileRow({
  profile,
  isActive,
  onDelete,
  deleteDisabled,
}: {
  profile: ModProfileDto
  isActive: boolean
  onDelete: () => void
  deleteDisabled: boolean
}) {
  return (
    <div
      className={`rounded-lg border px-4 py-3 flex items-start gap-3 ${
        isActive ? 'border-blue-600 bg-blue-950' : 'border-zinc-800 bg-zinc-900'
      }`}
    >
      <div className="flex-1 min-w-0 space-y-1">
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-zinc-100">{profile.name}</span>
          {isActive && (
            <span className="text-xs rounded px-1.5 py-0.5 bg-blue-600 text-white font-medium">Active</span>
          )}
        </div>
        {profile.notes && (
          <p className="text-xs text-zinc-400 truncate">{profile.notes}</p>
        )}
        <div className="flex items-center gap-3 text-xs text-zinc-500">
          <span>{profile.entryCount} mods</span>
          <span>Created {new Date(profile.createdAt).toLocaleDateString()}</span>
          {profile.lastUsedAt && (
            <span>Last used {new Date(profile.lastUsedAt).toLocaleDateString()}</span>
          )}
        </div>
      </div>
      <button
        className="btn-secondary shrink-0 text-sm"
        onClick={onDelete}
        disabled={deleteDisabled}
      >
        Delete
      </button>
    </div>
  )
}
