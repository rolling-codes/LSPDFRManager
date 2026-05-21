import { api } from './client'
import type { InstalledModDto, ModsListResponse } from '../../types/library'

export function fetchMods(params?: { search?: string; enabled?: boolean; type?: string }): Promise<ModsListResponse> {
  const query = new URLSearchParams()
  if (params?.search) query.set('search', params.search)
  if (params?.enabled !== undefined) query.set('enabled', String(params.enabled))
  if (params?.type) query.set('type', params.type)
  const qs = query.toString()
  return api.get<ModsListResponse>(`/api/v1/mods${qs ? `?${qs}` : ''}`)
}

export function toggleMod(id: string, enabled: boolean): Promise<InstalledModDto> {
  return api.post<InstalledModDto>(`/api/v1/mods/${id}/enable`, { enabled })
}

export function updateModNotes(id: string, notes: string): Promise<InstalledModDto> {
  return api.put<InstalledModDto>(`/api/v1/mods/${id}/notes`, { notes })
}
